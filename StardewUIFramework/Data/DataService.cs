using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Data
{
    /// <summary>
    /// Data-driven UIs: loads the <c>Menus</c>, <c>Huds</c>, <c>Owners</c>, <c>Sprites</c> and (v1.7) <c>Composites</c> /
    /// <c>Contributions</c> assets, validates them,
    /// builds each entry through its owner's <see cref="StardewUIApi"/> facade and keeps them in sync. Invalidation only
    /// sets a dirty flag; the assets are re-read on the next <c>UpdateTicked</c>, every entry is diffed by content hash,
    /// and changed menus / HUDs are rebuilt in place (<see cref="UIMenu.RebuildInPlace"/>) so an open menu stays open.
    /// Values are expressions (<see cref="ExpressionValueResolver"/>) over the named state of <see cref="DataStateStore"/>.
    /// <para>
    /// Collision rule: a menu a C# mod created with the same owner and id wins; the data entry is skipped with a
    /// warning. Opening through data (actions, tile actions, console) checks the entry's <c>Condition</c> and, without
    /// <c>force</c>, waits until the player is free.
    /// </para>
    /// </summary>
    internal sealed class DataService
    {
        private readonly IMonitor monitor;
        private readonly ConsumerContexts contexts;
        private readonly MenuRegistry menus;
        private readonly HotkeyService hotkeys;
        private readonly CompositeRegistry composites;
        private readonly ExtensionRegistry extensions;
        private readonly DataAssetReader reader;
        private readonly SpriteRefs sprites = new();
        private readonly DataValidator validator;
        private readonly Dictionary<string, StardewUIApi> facades = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DataMenuRuntime> built = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DataHudRuntime> huds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EntryStatus> statuses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> reported = new(StringComparer.Ordinal);
        private readonly PerScreen<List<string>> pendingOpens = new(() => new List<string>());
        private readonly Dictionary<string, OwnerDefinition> owners = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> ownerHashes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> ownerHotkeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, OwnerOverrides> ownerOverrides = new(StringComparer.OrdinalIgnoreCase);
        private readonly DataBuilder builder;
        private readonly HudBuilder hudBuilder;
        private readonly DataComposites dataComposites;
        private readonly ContributionBuilder contributions;
        private Dictionary<string, DataCompositeDefinition> compositeDefinitions = new(StringComparer.Ordinal);
        private bool dirty = true;
        private uint lastReloadTick = uint.MaxValue;

        internal DataService(IModHelper helper, IMonitor monitor, ConsumerContexts contexts, MenuRegistry menus, HotkeyService hotkeys, CompositeRegistry composites, ExtensionRegistry extensions)
        {
            this.monitor = monitor;
            this.contexts = contexts;
            this.menus = menus;
            this.hotkeys = hotkeys;
            this.composites = composites;
            this.extensions = extensions;
            reader = new DataAssetReader(helper.GameContent);
            validator = new DataValidator(id => helper.ModRegistry.IsLoaded(id), sprites, OwnerOf, name => compositeDefinitions.TryGetValue(name, out DataCompositeDefinition? def) ? def : null);

            State = new DataStateStore(contexts.For, new ConfigStore(helper.Data, monitor));
            State.Changed += OnStateChanged;
            DataStateStore.Active = State;
            DataScope.ContextResolver = contexts.For;
            ScopeRoots.RuntimeResolver = RuntimeByStateKey;
            ScopeRoots.Exposures = extensions.ExposuresOf;
            GameFunctions.Register(FunctionRegistry.Default, this);

            DataActionRunner.Resolver = Resolver;
            builder = new DataBuilder(Resolver, sprites, OwnerOf, State);
            hudBuilder = new HudBuilder(builder, Resolver);
            dataComposites = new DataComposites(composites, contexts, FacadeFor, builder);
            contributions = new ContributionBuilder(builder, Resolver, contexts, FacadeFor, extensions, menus);

            // a data menu destroyed from C# is built again on the next reload (unless a C# menu takes its key)
            menus.MenuUnregistered += menu =>
            {
                if (built.Values.Any(r => r.Menu == menu))
                {
                    dirty = true;
                }
            };

            // v1.6: the C# bridge (row sources, and a rebuild when models / composites data builds against change)
            if (UIServices.Hooks is { } hooks)
            {
                SourceBinding.HookResolver = hooks.ResolveRows;
                SourceBinding.HookVersion = hooks.RowsVersion;
                hooks.StructureChanged += () => dirty = true;
            }
        }

        /// <summary>The structure version of the C# bridge (part of every entry's hash: a changed model or composite rebuilds).</summary>
        private static string HooksHash => (UIServices.Hooks?.StructureVersion ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The value resolver of every data value (expressions from v1.4).</summary>
        internal IValueResolver Resolver { get; } = ExpressionValueResolver.Instance;

        /// <summary>The named state of data UIs.</summary>
        internal DataStateStore State { get; }

        /// <summary>The state of one entry after the last reload, for <c>ui_data</c> / <c>ui_validate</c>.</summary>
        internal sealed class EntryStatus
        {
            internal string Key = string.Empty;
            internal string State = string.Empty;
            internal string Hash = string.Empty;
            internal DataMessageLog Messages = new();
        }

        /// <summary>Messages of the assets themselves (load failures, sprites, owners).</summary>
        internal DataMessageLog AssetMessages { get; private set; } = new();

        /// <summary>Entry statuses of the last reload, by key (HUD keys are prefixed <c>hud:</c>).</summary>
        internal IEnumerable<EntryStatus> Statuses => statuses.Values.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase);

        /// <summary>The sprite registry (image references).</summary>
        internal SpriteRefs Sprites => sprites;

        /// <summary>The data menu built for <paramref name="key"/>, if any.</summary>
        internal DataMenuRuntime? Runtime(string key) => built.TryGetValue(key, out DataMenuRuntime? runtime) ? runtime : null;

        /// <summary>The data HUD built for <paramref name="key"/>, if any.</summary>
        internal DataHudRuntime? HudRuntime(string key) => huds.TryGetValue(key, out DataHudRuntime? runtime) ? runtime : null;

        /// <summary>The owner settings of <paramref name="owner"/> (the <c>Owners</c> asset), if any.</summary>
        internal OwnerDefinition? OwnerOf(string owner) => owners.TryGetValue(owner, out OwnerDefinition? def) ? def : null;

        /// <summary>The data UI whose <c>menu.*</c> container is <paramref name="stateKey"/> (<c>owner/menu</c> or <c>owner/hud:id</c>).</summary>
        private DataRuntime? RuntimeByStateKey(string stateKey)
        {
            if (built.TryGetValue(stateKey, out DataMenuRuntime? menu))
            {
                return menu;
            }

            int marker = stateKey.IndexOf("/hud:", StringComparison.Ordinal);
            return marker > 0 && huds.TryGetValue(stateKey.Substring(0, marker) + "/" + stateKey.Substring(marker + 5), out DataHudRuntime? hud) ? hud : null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------------------------------------------------

        internal void OnAssetRequested(object? sender, AssetRequestedEventArgs e) => DataAssets.OnAssetRequested(e);

        internal void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
        {
            if (e.NamesWithoutLocale.Any(IsWatched))
            {
                dirty = true;
            }
        }

        internal void OnAssetReady(object? sender, AssetReadyEventArgs e)
        {
            if (IsWatched(e.NameWithoutLocale))
            {
                dirty = true;
            }
        }

        internal void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // the assets are shared by every split-screen player: reload once per tick
            Bridge.DataImport.Poll(); // watched ImportDataFile files that were saved (re-imports invalidate the assets)
            if (dirty && lastReloadTick != e.Ticks)
            {
                lastReloadTick = e.Ticks;
                Reload();
            }

            if (Context.ScreenId == 0)
            {
                State.Config.OnUpdateTicked();
            }

            ProcessPendingOpens();
        }

        internal void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            pendingOpens.ResetAllScreens();
            State.ClearSession();
            RowScope.ItemCache.Clear();
            State.Config.Flush();
        }

        private bool IsWatched(IAssetName name)
        {
            return name.IsEquivalentTo(DataAssets.Menus) || name.IsEquivalentTo(DataAssets.Sprites) || name.IsEquivalentTo(DataAssets.Huds)
                || name.IsEquivalentTo(DataAssets.Owners) || name.IsEquivalentTo(DataAssets.Composites) || name.IsEquivalentTo(DataAssets.Contributions)
                || reader.FromAssets.Any(f => name.IsEquivalentTo(f));
        }

        /// <summary>Force the assets to be re-read on the next tick (<c>ui_reload</c>).</summary>
        internal void MarkDirty() => dirty = true;

        /// <summary>Run the watches of every data UI on a state change.</summary>
        private void OnStateChanged(int screen, StateAddress address, DataValue oldValue, DataValue newValue)
        {
            foreach (DataRuntime runtime in built.Values.Cast<DataRuntime>().Concat(huds.Values).ToArray())
            {
                runtime.OnStateChanged(address, oldValue, newValue);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Reload
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Re-read the assets, validate every entry and (re)build the changed ones. Returns the number of menus and HUDs built or rebuilt.</summary>
        internal int Reload()
        {
            dirty = false;
            statuses.Clear();
            var assetLog = new DataMessageLog();
            Dictionary<string, SpriteDefinition> spriteDefs = reader.ReadSprites(assetLog);
            validator.ValidateSprites(spriteDefs, assetLog);
            string previousSprites = sprites.Hash;
            sprites.Load(spriteDefs);
            bool spritesChanged = previousSprites != sprites.Hash;

            ReloadOwners(assetLog);
            int changed = ReloadComposites(assetLog);

            var logs = new Dictionary<string, DataMessageLog>(StringComparer.Ordinal);
            Dictionary<string, MenuDefinition> entries = reader.ReadMenus(logs, assetLog);
            var hudLogs = new Dictionary<string, DataMessageLog>(StringComparer.Ordinal);
            Dictionary<string, HudDefinition> hudEntries = reader.ReadHuds(hudLogs, assetLog);
            AssetMessages = assetLog;
            Report("*assets", DefinitionHash.Of(assetLog.Items.Select(m => m.ToString()).ToArray()), assetLog);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, DataMessageLog readLog) in logs)
            {
                var status = new EntryStatus { Key = key, Messages = readLog };
                statuses[key] = status;
                if (!entries.TryGetValue(key, out MenuDefinition? def))
                {
                    status.State = "not readable";
                    Report(key, string.Empty, readLog);
                    continue;
                }

                try
                {
                    if (Apply(key, def, status, spritesChanged, seen))
                    {
                        changed++;
                    }
                }
                catch (Exception ex)
                {
                    status.Messages.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Menus), key), $"could not be built: {ex.Message}");
                    status.State = "failed";
                    monitor.Log($"Data menu '{key}' failed to build:\n{ex}", LogLevel.Trace);
                }

                Report(key, status.Hash + "|" + status.State.Replace(" (open)", string.Empty).Replace("rebuilt", "built"), status.Messages);
            }

            // entries that disappeared
            foreach (string key in built.Keys.Where(k => !seen.Contains(k)).ToArray())
            {
                Remove(key);
            }

            changed += ReloadHuds(hudEntries, hudLogs, spritesChanged);
            changed += ReloadContributions(assetLog);
            State.BumpGlobal();

            if (changed > 0)
            {
                monitor.Log($"Data UIs: {changed} built or rebuilt, {built.Count} menu(s), {huds.Count} HUD(s), {dataComposites.Runtimes.Count} composite(s) and {contributions.Runtimes.Count} contribution(s) loaded.", LogLevel.Trace);
            }

            return changed;
        }

        /// <summary>Validate and build one entry; true when it was built or rebuilt.</summary>
        private bool Apply(string key, MenuDefinition def, EntryStatus status, bool spritesChanged, HashSet<string> seen)
        {
            if (!validator.ValidateMenu(key, def, status.Messages, out string owner, out string menuId))
            {
                status.Hash = DefinitionHash.Of(def, sprites.Hash);
                status.State = "rejected";
                return false;
            }

            string hash = DefinitionHash.Of(def, sprites.Hash, OwnerHash(owner), HooksHash);
            status.Hash = hash;
            string runtimeKey = owner + "/" + menuId;
            seen.Add(runtimeKey);
            UIMenu? existing = menus.Get(owner, menuId);
            built.TryGetValue(runtimeKey, out DataMenuRuntime? runtime);

            // C# menus win
            if (existing != null && (runtime == null || runtime.Menu != existing))
            {
                if (runtime != null)
                {
                    built.Remove(runtimeKey); // the C# mod replaced our menu
                }

                status.Messages.Warn(DataPath.Entry(DataAssets.ShortName(DataAssets.Menus), key), $"'{owner}' already created menu '{menuId}' in C#; the C# menu wins and this entry is skipped.");
                status.State = "skipped (C# menu)";
                seen.Remove(runtimeKey);
                return false;
            }

            // the managed menu is no longer registered (a C# DestroyMenu): build a new one, whatever the hash
            bool destroyed = runtime != null && existing != runtime.Menu;
            if (runtime != null && !destroyed && runtime.Hash == hash && !spritesChanged)
            {
                status.Messages = runtime.Messages;
                status.State = runtime.Menu?.IsOpen == true ? "built (open)" : "built";
                return false;
            }

            StardewUIApi api = FacadeFor(owner);
            if (runtime == null || destroyed)
            {
                runtime ??= new DataMenuRuntime(owner, menuId);
                runtime.Definition = def;
                runtime.Hash = hash;
                runtime.Messages = status.Messages;
                var menu = (UIMenu)api.CreateMenu(menuId);
                runtime.Menu = menu;
                built[runtimeKey] = runtime;
                builder.BuildMenu(menu, api, runtime);
                status.State = "built";
                return true;
            }

            runtime.Definition = def;
            runtime.Hash = hash;
            runtime.Messages = status.Messages;
            UIMenu target = runtime.Menu!;
            DataMenuRuntime current = runtime;
            target.RebuildInPlace(m => builder.BuildMenu(m, api, current));
            status.State = target.IsOpen ? "rebuilt (open)" : "rebuilt";
            return true;
        }

        private void Remove(string key)
        {
            if (!built.Remove(key, out DataMenuRuntime? runtime) || runtime.Menu == null)
            {
                return;
            }

            StardewUIApi api = FacadeFor(runtime.Owner);
            if (menus.Get(runtime.Owner, runtime.MenuId) == runtime.Menu)
            {
                if (runtime.HotkeyBound)
                {
                    api.BindToggleHotkey(runtime.Menu, string.Empty);
                }

                api.DestroyMenu(runtime.MenuId);
            }

            monitor.Log($"Data menu '{key}' was removed from {DataAssets.Menus}.", LogLevel.Trace);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  HUDs
        // ---------------------------------------------------------------------------------------------------------

        private int ReloadHuds(Dictionary<string, HudDefinition> entries, Dictionary<string, DataMessageLog> logs, bool spritesChanged)
        {
            int changed = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, DataMessageLog readLog) in logs)
            {
                string statusKey = "hud:" + key;
                var status = new EntryStatus { Key = statusKey, Messages = readLog };
                statuses[statusKey] = status;
                if (!entries.TryGetValue(key, out HudDefinition? def))
                {
                    status.State = "not readable";
                    Report(statusKey, string.Empty, readLog);
                    continue;
                }

                try
                {
                    if (ApplyHud(key, def, status, spritesChanged, seen))
                    {
                        changed++;
                    }
                }
                catch (Exception ex)
                {
                    status.Messages.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key), $"could not be built: {ex.Message}");
                    status.State = "failed";
                    monitor.Log($"Data HUD '{key}' failed to build:\n{ex}", LogLevel.Trace);
                }

                Report(statusKey, status.Hash + "|" + status.State.Replace("rebuilt", "built"), status.Messages);
            }

            foreach (string key in huds.Keys.Where(k => !seen.Contains(k)).ToArray())
            {
                huds.Remove(key, out DataHudRuntime? runtime);
                if (runtime != null)
                {
                    StardewUIApi api = FacadeFor(runtime.Owner);
                    api.UnregisterHotkey(HudBuilder.HotkeyId(runtime.Id));
                    api.DestroyHud(runtime.Id);
                    monitor.Log($"Data HUD '{key}' was removed from {DataAssets.Huds}.", LogLevel.Trace);
                }
            }

            return changed;
        }

        private bool ApplyHud(string key, HudDefinition def, EntryStatus status, bool spritesChanged, HashSet<string> seen)
        {
            if (!validator.ValidateHud(key, def, status.Messages, out string owner, out string hudId))
            {
                status.State = "rejected";
                return false;
            }

            string runtimeKey = owner + "/" + hudId;
            string hash = DefinitionHash.Of(def, sprites.Hash, OwnerHash(owner), HooksHash);
            status.Hash = hash;
            seen.Add(runtimeKey);
            huds.TryGetValue(runtimeKey, out DataHudRuntime? runtime);
            StardewUIApi api = FacadeFor(owner);
            UIHud? existing = UIServices.Hud?.Get(owner, hudId);
            if (existing != null && (runtime == null || runtime.Hud != existing))
            {
                status.Messages.Warn(DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key), $"'{owner}' already created HUD '{hudId}' in C#; the C# widget wins and this entry is skipped.");
                status.State = "skipped (C# HUD)";
                huds.Remove(runtimeKey);
                seen.Remove(runtimeKey);
                return false;
            }

            if (runtime != null && runtime.Hash == hash && !spritesChanged)
            {
                status.Messages = runtime.Messages;
                status.State = "built";
                return false;
            }

            bool rebuild = runtime != null;
            runtime ??= new DataHudRuntime(owner, hudId);
            runtime.Definition = def;
            runtime.Hash = hash;
            runtime.Messages = status.Messages;
            huds[runtimeKey] = runtime;
            hudBuilder.Build(api, runtime);
            status.State = rebuild ? "rebuilt" : "built";
            return true;
        }

        /// <summary>Show (true), hide (false) or toggle (null) a data HUD for the current player.</summary>
        internal bool SetHudVisible(string key, bool? visible, out string error)
        {
            error = string.Empty;
            if (!huds.TryGetValue(key.Trim(), out DataHudRuntime? runtime))
            {
                error = $"no data HUD '{key}' is loaded (expected '<owner>/<hud id>' from {DataAssets.Huds}).";
                return false;
            }

            runtime.Visible = visible ?? !runtime.Visible;
            return true;
        }

        /// <summary>True when a data HUD is shown for the current player (its visibility and ShowWhen).</summary>
        internal bool IsHudShown(string key)
        {
            if (!huds.TryGetValue(key.Trim(), out DataHudRuntime? runtime) || runtime.Hud == null)
            {
                return false;
            }

            return runtime.Visible && runtime.Hud.ShowWhen?.Invoke() != false;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Composites and contributions (v1.7)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The loaded data composites (for <c>ui_data</c>).</summary>
        internal DataComposites Composites => dataComposites;

        /// <summary>The loaded contributions (for <c>ui_data</c>).</summary>
        internal ContributionBuilder Contributions => contributions;

        /// <summary>Read the <c>Composites</c> asset: register new / changed data composites (their live instances rebuild) and drop removed ones.</summary>
        private int ReloadComposites(DataMessageLog assetLog)
        {
            compositeDefinitions = reader.ReadComposites(assetLog);
            int changed = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string name, DataCompositeDefinition def) in compositeDefinitions)
            {
                string statusKey = "composite:" + name;
                var status = new EntryStatus { Key = statusKey };
                statuses[statusKey] = status;
                try
                {
                    if (!validator.ValidateComposite(name, def, status.Messages, out string owner))
                    {
                        status.State = "rejected";
                        status.Hash = DefinitionHash.Of(def);
                    }
                    else
                    {
                        status.Hash = DefinitionHash.Of(def, OwnerHash(owner), sprites.Hash, HooksHash);
                        status.State = dataComposites.Apply(name, owner, def, status.Hash, status.Messages);
                        if (!status.State.StartsWith("skipped", StringComparison.Ordinal))
                        {
                            seen.Add(name);
                        }

                        if (status.State.StartsWith("rebuilt", StringComparison.Ordinal) || status.State.Contains("instance", StringComparison.Ordinal))
                        {
                            changed++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    status.Messages.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Composites), name), $"could not be loaded: {ex.Message}");
                    status.State = "failed";
                    monitor.Log($"Data composite '{name}' failed to load:\n{ex}", LogLevel.Trace);
                }

                Report(statusKey, status.Hash + "|" + status.State, status.Messages);
            }

            dataComposites.RemoveMissing(seen, monitor);
            return changed;
        }

        /// <summary>Read the <c>Contributions</c> asset and register the valid entries through their contributors' facades.</summary>
        private int ReloadContributions(DataMessageLog assetLog)
        {
            Dictionary<string, ContributionDefinition> definitions = reader.ReadContributions(assetLog);
            var entries = new List<ContributionBuilder.Entry>();
            foreach ((string key, ContributionDefinition def) in definitions)
            {
                string statusKey = "contribution:" + key;
                var status = new EntryStatus { Key = statusKey };
                statuses[statusKey] = status;
                try
                {
                    if (!validator.ValidateContribution(key, def, status.Messages, out string contributor, out string name, out string targetOwner, out string targetMenu, out int priority))
                    {
                        status.State = "rejected";
                        status.Hash = DefinitionHash.Of(def);
                    }
                    else
                    {
                        status.Hash = DefinitionHash.Of(def, OwnerHash(contributor), sprites.Hash, HooksHash);
                        entries.Add(new ContributionBuilder.Entry(key, contributor, name, targetOwner, targetMenu, priority, def, status.Hash, status.Messages));
                        status.State = $"registered ({targetOwner}/{targetMenu})";
                    }
                }
                catch (Exception ex)
                {
                    status.Messages.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Contributions), key), $"could not be loaded: {ex.Message}");
                    status.State = "failed";
                    monitor.Log($"Contribution '{key}' failed to load:\n{ex}", LogLevel.Trace);
                }

                Report(statusKey, status.Hash + "|" + status.State, status.Messages);
            }

            return contributions.Apply(entries);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Owners
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Read the <c>Owners</c> asset and apply each owner's tooltip delay, default style and hotkeys.</summary>
        private void ReloadOwners(DataMessageLog log)
        {
            Dictionary<string, OwnerDefinition> definitions = reader.ReadOwners(log);
            var valid = new Dictionary<string, OwnerDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach ((string owner, OwnerDefinition def) in definitions)
            {
                if (validator.ValidateOwner(owner, def, log))
                {
                    valid[owner] = def;
                }
            }

            // owners that were removed go back to their C# values
            foreach (string owner in owners.Keys.Where(o => !valid.ContainsKey(o)).ToArray())
            {
                ApplyOwner(owner, null, log);
                owners.Remove(owner);
                ownerHashes.Remove(owner);
            }

            foreach ((string owner, OwnerDefinition def) in valid)
            {
                string hash = DefinitionHash.Of(def);
                if (ownerHashes.TryGetValue(owner, out string? previous) && previous == hash)
                {
                    continue;
                }

                owners[owner] = def;
                ownerHashes[owner] = hash;
                ApplyOwner(owner, def, log);
            }
        }

        private string OwnerHash(string owner) => ownerHashes.TryGetValue(owner, out string? hash) ? hash : string.Empty;

        private void ApplyOwner(string owner, OwnerDefinition? def, DataMessageLog log)
        {
            ConsumerContext context = contexts.For(owner);
            StardewUIApi api = FacadeFor(owner);
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Owners), owner);
            DataScope scope = DataScope.ForOwner(owner);

            // only the fields the entry sets are applied; the C# values (SetTooltipDelay / SetDefaultStyle) are captured
            // before data first overrides them and restored when the entry stops setting them (or is removed)
            if (!ownerOverrides.TryGetValue(owner, out OwnerOverrides? overrides))
            {
                ownerOverrides[owner] = overrides = new OwnerOverrides();
            }

            if (def?.TooltipDelayMs != null && ValueParsers.Int.Parse(def.TooltipDelayMs, out int delay) && delay >= 0)
            {
                if (!overrides.DelaySet)
                {
                    overrides.Delay = context.TooltipDelayMs;
                    overrides.DelaySet = true;
                }

                context.TooltipDelayMs = delay;
            }
            else if (overrides.DelaySet)
            {
                context.TooltipDelayMs = overrides.Delay;
                overrides.DelaySet = false;
            }

            if (def?.DefaultStyle != null)
            {
                if (!overrides.StyleSet)
                {
                    overrides.Style = context.DefaultStyle;
                    overrides.StyleSet = true;
                }

                var applier = new PropertyApplier(LiteralValueResolver.Instance, new RefresherGroup(owner, "DefaultStyle"), log);
                api.SetDefaultStyle(builder.BuildStyle(api, def.DefaultStyle, scope, path.Field("DefaultStyle"), applier));
            }
            else if (overrides.StyleSet)
            {
                context.DefaultStyle = overrides.Style;
                overrides.StyleSet = false;
            }

            if (ownerHotkeys.TryGetValue(owner, out List<string>? previous))
            {
                foreach (string id in previous)
                {
                    api.UnregisterHotkey(id);
                }
            }

            var registered = new List<string>();
            if (def?.Hotkeys != null)
            {
                foreach ((string id, HotkeyDefinition? hotkey) in def.Hotkeys)
                {
                    if (hotkey?.Keys == null || !ValueParsers.Keybind.Parse(hotkey.Keys, out _))
                    {
                        continue;
                    }

                    string hotkeyId = "data:" + id;
                    HotkeyDefinition current = hotkey;
                    api.RegisterHotkey(hotkeyId, hotkey.Keys, () =>
                    {
                        if (DataActionRunner.CheckCondition(current.Condition))
                        {
                            DataActionRunner.Run(current.Actions, scope.WithEvent("Hotkey", null));
                        }
                    });
                    registered.Add(hotkeyId);
                }
            }

            ownerHotkeys[owner] = registered;
        }

        /// <summary>The owner's C# values an <c>Owners</c> entry replaced, restored when the entry stops setting them.</summary>
        private sealed class OwnerOverrides
        {
            internal bool DelaySet;
            internal int? Delay;
            internal bool StyleSet;
            internal UIStyle? Style;
        }

        /// <summary>The owner's facade, constructed exactly as <see cref="ModEntry.GetApi"/> constructs it (shared context).</summary>
        private StardewUIApi FacadeFor(string owner)
        {
            if (!facades.TryGetValue(owner, out StardewUIApi? api))
            {
                facades[owner] = api = new StardewUIApi(contexts.For(owner), menus, hotkeys, composites, extensions);
            }

            return api;
        }

        /// <summary>Log an entry's messages, once per version of the entry (Content Patcher re-invalidates unchanged assets at day start).</summary>
        private void Report(string key, string version, DataMessageLog log)
        {
            if (reported.TryGetValue(key, out string? last) && last == version)
            {
                return;
            }

            reported[key] = version;
            foreach (DataMessage message in log.Items)
            {
                monitor.Log(message.ToString(), message.Severity switch
                {
                    DataSeverity.Error => LogLevel.Error,
                    DataSeverity.Warning => LogLevel.Warn,
                    _ => LogLevel.Trace
                });
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Opening / closing (trigger actions, tile actions, console)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The menu registered as <c>owner/menu</c> (data or C#), or null.</summary>
        internal UIMenu? Find(string key)
        {
            return DataValidator.TrySplitKey(key ?? string.Empty, out string owner, out string id) ? menus.Get(owner, id) : null;
        }

        /// <summary>The topmost open framework menu on this screen, or null.</summary>
        internal UIMenu? Topmost => menus.OpenMenus.Count > 0 ? menus.OpenMenus[^1] : null;

        /// <summary>Re-evaluate a menu's open-time values (<c>Condition</c>s, one-time values) and lay it out again (<c>_Refresh</c>).</summary>
        internal void RefreshMenu(UIMenu menu)
        {
            RuntimeOf(menu)?.Refresh(menu, opening: true);
            menu.InvalidateLayout();
        }

        /// <summary>The data runtime whose tree lives in <paramref name="menu"/> (a data menu, or a data HUD's inner menu), or null.</summary>
        internal DataRuntime? RuntimeOf(UIMenu menu)
        {
            return built.Values.FirstOrDefault(r => r.Menu == menu) ?? (DataRuntime?)huds.Values.FirstOrDefault(h => h.Model == menu);
        }

        /// <summary>Open a menu; without <paramref name="force"/> it waits until the player is free. False (with an error) when it cannot.</summary>
        internal bool Open(string key, bool force, out string error)
        {
            if (!TryGetOpenable(key, out UIMenu? menu, out error))
            {
                return false;
            }

            if (menu.IsOpen)
            {
                return true;
            }

            if (!force && !Context.IsPlayerFree)
            {
                Queue(key);
                return true;
            }

            menu.Open(true);
            return true;
        }

        /// <summary>Open a menu as a child of <paramref name="parentKey"/> (default: the topmost open framework menu).</summary>
        internal bool OpenAsChild(string key, string? parentKey, out string error)
        {
            if (!TryGetOpenable(key, out UIMenu? menu, out error))
            {
                return false;
            }

            UIMenu? parent = string.IsNullOrWhiteSpace(parentKey) ? Topmost : Find(parentKey);
            if (parent == null || !parent.IsOpen)
            {
                error = string.IsNullOrWhiteSpace(parentKey) ? "no framework menu is open to be the parent." : $"the parent menu '{parentKey}' is not open.";
                return false;
            }

            if (parent == menu)
            {
                error = "a menu cannot be its own parent.";
                return false;
            }

            menu.OpenAsChild(parent);
            return true;
        }

        /// <summary>Close a menu (default: the topmost open framework menu).</summary>
        internal bool Close(string? key, out string error)
        {
            error = string.Empty;
            UIMenu? menu = string.IsNullOrWhiteSpace(key) ? Topmost : Find(key);
            if (menu == null)
            {
                error = string.IsNullOrWhiteSpace(key) ? "no framework menu is open." : $"no menu '{key}' is registered.";
                return false;
            }

            menu.Close();
            return true;
        }

        /// <summary>Close the menu when open, else open it (waiting until the player is free).</summary>
        internal bool Toggle(string key, out string error)
        {
            UIMenu? menu = Find(key);
            if (menu != null && menu.IsOpen)
            {
                error = string.Empty;
                menu.Close();
                return true;
            }

            return Open(key, force: false, out error);
        }

        /// <summary>True when the menu is open on this screen.</summary>
        internal bool IsOpen(string key) => Find(key)?.IsOpen ?? false;

        private bool TryGetOpenable(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UIMenu? menu, out string error)
        {
            error = string.Empty;
            menu = Find(key);
            if (menu == null)
            {
                error = $"no menu '{key}' is registered (expected '<owner>/<menu id>').";
                return false;
            }

            if (built.TryGetValue(key, out DataMenuRuntime? runtime) && runtime.Menu == menu && !DataActionRunner.CheckCondition(runtime.Definition.Condition))
            {
                error = $"the Condition of menu '{key}' does not match.";
                return false;
            }

            return true;
        }

        private void Queue(string key)
        {
            List<string> queue = pendingOpens.Value;
            if (!queue.Contains(key))
            {
                queue.Add(key);
                monitor.Log($"Menu '{key}' will open when the player is free.", LogLevel.Trace);
            }
        }

        private void ProcessPendingOpens()
        {
            List<string> queue = pendingOpens.Value;
            if (queue.Count == 0 || !Context.IsPlayerFree)
            {
                return;
            }

            string key = queue[0];
            queue.RemoveAt(0);
            if (!Open(key, force: true, out string error))
            {
                monitor.Log($"Queued menu '{key}' could not open: {error}", LogLevel.Warn);
            }
        }
    }
}
