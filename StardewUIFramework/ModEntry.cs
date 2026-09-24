using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data;
using UIFramework.Data.Actions;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework
{
    /// <summary>The mod entry point. Wires the framework services and hands out one <see cref="IStardewUIApi"/> per consumer mod.</summary>
    public class ModEntry : Mod
    {
        private readonly MenuRegistry menus = new();
        private readonly CompositeRegistry composites = new();
        private readonly ConsumerContexts contexts = new();
        private DataService? data;
        private HotkeyService hotkeys = null!;
        private ExtensionRegistry extensions = null!;
        private ModConfig config = new();

        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<ModConfig>();

            UIServices.Monitor = Monitor;
            UIServices.Config = config;
            UIServices.ModContent = helper.ModContent;
            UIServices.GameContent = helper.GameContent;
            UIServices.Translation = helper.Translation;

            hotkeys = new HotkeyService(helper.Events);

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.SaveLoaded += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            helper.ConsoleCommands.Add("ui_debug", "Toggle the UI Framework debug overlay (element bounds and ids).", (_, _) =>
            {
                config.DebugOverlay = !config.DebugOverlay;
                Monitor.Log($"Debug overlay {(config.DebugOverlay ? "enabled" : "disabled")}.", LogLevel.Info);
            });

            // v1.1 wiring (one region per feature; see architecture.md §16)

            // BEGIN SLOTS entry
            extensions = new ExtensionRegistry(menus);
            helper.ConsoleCommands.Add("ui_slots", "Print the extension slots of every open UI Framework menu (hints and contributors).", (_, _) =>
            {
                Monitor.Log(extensions.DescribeOpenMenus(), LogLevel.Info);
            });
            // END SLOTS entry

            // BEGIN COMPOSITES entry
            helper.ConsoleCommands.Add("ui_composites", "List the composite components defined through the UI Framework.", (_, _) =>
            {
                string[] names = composites.List();
                Monitor.Log(names.Length == 0 ? "No composites are defined." : $"Composites:\n  {string.Join("\n  ", names)}", LogLevel.Info);
            });
            // END COMPOSITES entry

            // BEGIN RICHTEXT entry
            helper.ConsoleCommands.Add("ui_pseudoloc", "Toggle UI Framework pseudo-localization (accented, padded strings in every framework menu).", (_, _) =>
            {
                config.PseudoLocalize = !config.PseudoLocalize;
                Monitor.Log($"Pseudo-localization {(config.PseudoLocalize ? "enabled" : "disabled")}.", LogLevel.Info);
            });
            // END RICHTEXT entry

            // BEGIN THEME entry
            UIServices.SaveConfig = () => Helper.WriteConfig(config);
            ThemeSwitcher.Menus = menus;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += (_, e) => RefreshThemeIf(e.NamesWithoutLocale.Any(n => n.IsEquivalentTo(Theme.AssetName)));
            helper.Events.Content.AssetReady += (_, e) => RefreshThemeIf(e.NameWithoutLocale.IsEquivalentTo(Theme.AssetName));
            helper.ConsoleCommands.Add("ui_theme", "Switch the UI Framework theme: ui_theme <name> (no argument lists the themes).", OnThemeCommand);
            // END THEME entrywww

            // BEGIN HUD entry
            var layouts = new WindowLayoutStore(helper.Data);
            var hud = new HudService(helper, menus);
            UIServices.Layouts = layouts;
            UIServices.Hud = hud;
            helper.Events.GameLoop.SaveLoaded += (_, _) =>
            {
                layouts.Load();
                hud.ApplyLayouts();
            };
            helper.Events.GameLoop.Saving += (_, _) => layouts.Save();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => layouts.Clear();
            helper.ConsoleCommands.Add("ui_toast", "Show a test toast: ui_toast <text>.", (_, args) =>
            {
                hud.ShowToast(args.Length == 0 ? "Hello from the UI Framework!" : string.Join(" ", args), null, null, ToastLayer.DefaultDurationMs);
            });
            helper.ConsoleCommands.Add("ui_layout_reset", "Forget every player-adjusted window position / size / collapsed state for the current save.", (_, _) =>
            {
                int count = layouts.Count;
                layouts.Clear();
                if (Context.IsWorldReady)
                {
                    layouts.Save();
                }

                Monitor.Log($"Cleared {count} saved window layout(s).", LogLevel.Info);
            });
            // END HUD entry

            // BEGIN DATA entry
            UIServices.Hooks = new HookRegistry();
            data = new DataService(helper, Monitor, contexts, menus, hotkeys, composites, extensions);
            UIServices.Data = data;
            helper.Events.Content.AssetRequested += data.OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += data.OnAssetsInvalidated;
            helper.Events.Content.AssetsInvalidated += (_, e) => TextureCache.Invalidate(e.NamesWithoutLocale);
            helper.Events.Content.AssetReady += data.OnAssetReady;
            helper.Events.GameLoop.UpdateTicked += data.OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += data.OnReturnedToTitle;
            FrameworkTriggerActions.Register(data);
            StateActions.Register(data);
            CollectionActions.Register(data);
            BridgeActions.Register(data);
            FrameworkQueries.Register(data);
            FrameworkTokens.Register(data);
            TileActions.Register(data, Monitor);
            // END DATA entry

            // BEGIN TOOLS entry
            Inspector.ExportDirectory = Path.Combine(helper.DirectoryPath, "export");
            new DebugConsole(menus, Monitor, data, Path.Combine(helper.DirectoryPath, "schema")).Register(helper.ConsoleCommands);
            helper.Events.Input.ButtonsChanged += (_, _) => Inspector.OnButtonsChanged();
            // END TOOLS entry
        }

        /// <summary>
        /// One API instance per request, over one shared <see cref="ConsumerContext"/> per consumer (so ids, hotkeys and
        /// styles are namespaced, and the C# and data halves of a hybrid mod share style, tooltip delay, bindings and mutes).
        /// </summary>
        public override object GetApi(IModInfo mod)
        {
            ConsumerContext consumer = contexts.For(mod.Manifest.UniqueID);
            Monitor.Log($"API requested by {mod.Manifest.UniqueID}.", LogLevel.Trace);
            return new StardewUIApi(consumer, menus, hotkeys, composites, extensions);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            menus.ValidateOpenMenus();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // BEGIN DATA launched
            ContentPatcherToken.Register(Helper, ModManifest, Monitor); // optional: {{6135.UIFramework/State: <owner>/<key>}}
            // END DATA launched

            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                return;
            }

            gmcm.Register(ModManifest, () => UIServices.Config = config = new ModConfig(), () => Helper.WriteConfig(config), titleScreenOnly: false);
            gmcm.AddNumberOption(ModManifest,
                getValue: () => config.TooltipDelayMs,
                setValue: v => config.TooltipDelayMs = v,
                name: () => Helper.Translation.Get("config.tooltip-delay"),
                tooltip: () => Helper.Translation.Get("config.tooltip-delay.desc"),
                min: 0, max: 3000, interval: 50, formatValue: null, fieldId: null);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.DebugOverlay,
                setValue: v => config.DebugOverlay = v,
                name: () => Helper.Translation.Get("config.debug-overlay"),
                tooltip: () => Helper.Translation.Get("config.debug-overlay.desc"),
                fieldId: null);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.LogCallbacks,
                setValue: v => config.LogCallbacks = v,
                name: () => Helper.Translation.Get("config.log-callbacks"),
                tooltip: () => Helper.Translation.Get("config.log-callbacks.desc"),
                fieldId: null);

            // BEGIN RICHTEXT gmcm
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.PseudoLocalize,
                setValue: v => config.PseudoLocalize = v,
                name: () => Helper.Translation.Get("config.pseudo-localize"),
                tooltip: () => Helper.Translation.Get("config.pseudo-localize.desc"),
                fieldId: null);
            // END RICHTEXT gmcm

            // BEGIN THEME gmcm
            // the theme list is read a few ticks later so Content Patcher (which initializes on the first tick) has added its themes
            int ticksUntilThemeOptions = 5;
            void AddThemeOptionsWhenReady(object? s, UpdateTickedEventArgs args)
            {
                if (--ticksUntilThemeOptions > 0)
                {
                    return;
                }

                Helper.Events.GameLoop.UpdateTicked -= AddThemeOptionsWhenReady;
                RegisterThemeOptions(gmcm);
            }
            Helper.Events.GameLoop.UpdateTicked += AddThemeOptionsWhenReady;
            // END THEME gmcm

            // BEGIN TOOLS gmcm
            gmcm.AddKeybindList(ModManifest,
                getValue: () => Inspector.ParseHotkey(config.InspectorHotkey),
                setValue: v => config.InspectorHotkey = v.ToString(),
                name: () => Helper.Translation.Get("config.inspector-hotkey"),
                tooltip: () => Helper.Translation.Get("config.inspector-hotkey.desc"),
                fieldId: null);
            // END TOOLS gmcm
        }

        // ---------------------------------------------------------------------------------------------------------
        //  THEME: theme asset, console command, screen reader
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Provide the bundled <c>assets/themes.json</c> as the default content of the theme asset (Content Patcher packs edit it).</summary>
        private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(Theme.AssetName))
            {
                e.LoadFromModFile<Dictionary<string, ThemeData>>("assets/themes.json", AssetLoadPriority.Exclusive);
            }
        }

        private static void RefreshThemeIf(bool themeAssetChanged)
        {
            if (themeAssetChanged)
            {
                ThemeSwitcher.Refresh();
            }
        }

        /// <summary>GMCM options for the theme, text scale and reduced motion settings.</summary>
        private void RegisterThemeOptions(IGenericModConfigMenuApi gmcm)
        {
            gmcm.AddTextOption(ModManifest,
                getValue: () => config.Theme,
                setValue: v => ThemeSwitcher.Apply(v ?? Theme.DefaultThemeName, ModManifest.UniqueID),
                name: () => Helper.Translation.Get("config.theme"),
                tooltip: () => Helper.Translation.Get("config.theme.desc"),
                allowedValues: ThemeChoices(), formatAllowedValue: null, fieldId: null);
            gmcm.AddNumberOption(ModManifest,
                getValue: () => config.TextScale,
                setValue: v =>
                {
                    config.TextScale = v;
                    ThemeSwitcher.Refresh();
                },
                name: () => Helper.Translation.Get("config.text-scale"),
                tooltip: () => Helper.Translation.Get("config.text-scale.desc"),
                min: 0.75f, max: 2f, interval: 0.05f, formatValue: v => v.ToString("0.00"), fieldId: null);
            gmcm.AddBoolOption(ModManifest,
                getValue: () => config.ReducedMotion,
                setValue: v => config.ReducedMotion = v,
                name: () => Helper.Translation.Get("config.reduced-motion"),
                tooltip: () => Helper.Translation.Get("config.reduced-motion.desc"),
                fieldId: null);
        }

        /// <summary>The theme names for the GMCM dropdown: everything in the asset plus the configured name (so an unknown value still shows).</summary>
        private string[] ThemeChoices()
        {
            var names = new List<string>(Theme.ThemeNames);
            if (!names.Contains(config.Theme, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(config.Theme);
            }

            return names.ToArray();
        }

        private void OnThemeCommand(string command, string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Monitor.Log($"Active theme: {Theme.ActiveName}. Available: {string.Join(", ", Theme.ThemeNames)}.", LogLevel.Info);
                return;
            }

            ThemeSwitcher.Apply(args[0], ModManifest.UniqueID);
        }
    }
}
