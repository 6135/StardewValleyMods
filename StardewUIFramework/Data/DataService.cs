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
    internal sealed partial class DataService
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
        private DataRuntime[] watchers = Array.Empty<DataRuntime>();
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
            State.SharesState = owner => OwnerOf(owner)?.SharedState is { } shared && ValueParsers.TryParseBool(shared, out bool yes) && yes;
            DataStateStore.Active = State;
            DataScope.ContextResolver = contexts.For;
            ScopeRoots.RuntimeResolver = RuntimeByStateKey;
            ScopeRoots.Exposures = extensions.ExposuresOf;
            GameFunctions.Register(FunctionRegistry.Default, this);

            // loc() and itemName() are cached like any value: a language change must re-evaluate them
            helper.Events.Content.LocaleChanged += (_, _) => State.BumpGlobal();

            builder = new DataBuilder(Resolver, sprites, OwnerOf, State);
            hudBuilder = new HudBuilder(builder, Resolver);
            dataComposites = new DataComposites(composites, contexts, FacadeFor, builder);
            contributions = new ContributionBuilder(builder, Resolver, contexts, FacadeFor, menus);

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
        internal ExpressionValueResolver Resolver => ExpressionValueResolver.Instance;

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
                State.RemoveDeadScreens();
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

        /// <summary>Run the watches of every data UI on a state change (the runtimes with watches, listed by the last reload).</summary>
        private void OnStateChanged(int screen, StateAddress address, DataValue oldValue, DataValue newValue)
        {
            foreach (DataRuntime runtime in watchers)
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
            sprites.Load(spriteDefs); // every entry hash includes the sprites' hash, so a sprite change rebuilds

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
                    if (Apply(key, def, status, seen))
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

            changed += ReloadHuds(hudEntries, hudLogs);
            changed += ReloadContributions(assetLog);
            watchers = built.Values.Cast<DataRuntime>().Concat(huds.Values).Where(r => r.HasWatches).ToArray();
            State.BumpGlobal();

            if (changed > 0)
            {
                monitor.Log($"Data UIs: {changed} built or rebuilt, {built.Count} menu(s), {huds.Count} HUD(s), {dataComposites.Runtimes.Count} composite(s) and {contributions.Runtimes.Count} contribution(s) loaded.", LogLevel.Trace);
            }

            return changed;
        }

        /// <summary>Validate and build one entry; true when it was built or rebuilt.</summary>
        private bool Apply(string key, MenuDefinition def, EntryStatus status, HashSet<string> seen)
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
            if (runtime != null && !destroyed && runtime.Hash == hash)
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
                runtime.Hash = string.Empty; // set once the build succeeded: a failed build is retried on the next reload
                runtime.Messages = status.Messages;
                var menu = (UIMenu)api.CreateMenu(menuId);
                runtime.Menu = menu;
                built[runtimeKey] = runtime;
                builder.BuildMenu(menu, api, runtime);
                runtime.Hash = hash;
                status.State = "built";
                return true;
            }

            runtime.Definition = def;
            runtime.Hash = string.Empty;
            runtime.Messages = status.Messages;
            UIMenu target = runtime.Menu!;
            DataMenuRuntime current = runtime;
            target.RebuildInPlace(m => builder.BuildMenu(m, api, current));
            runtime.Hash = hash;
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
                api.UnregisterHotkey(DataBuilder.MenuHotkeyId(runtime.MenuId));
                api.DestroyMenu(runtime.MenuId);
            }

            monitor.Log($"Data menu '{key}' was removed from {DataAssets.Menus}.", LogLevel.Trace);
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
    }
}
