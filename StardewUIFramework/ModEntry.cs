using System.IO;
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
        private HotkeyService hotkeys = null!;
        private ExtensionRegistry extensions = null!;

        public override void Entry(IModHelper helper)
        {
            UIServices.Monitor = Monitor;
            UIServices.Config = helper.ReadConfig<ModConfig>();
            UIServices.SaveConfig = () => helper.WriteConfig(UIServices.Config);
            UIServices.GameContent = helper.GameContent;
            UIServices.Translation = helper.Translation;
            StardewUIApi.Version = ModManifest.Version.ToString();

            hotkeys = new HotkeyService(helper.Events);
            menus.MenuUnregistered += hotkeys.ForgetMenu; // toggle hotkeys of a destroyed or replaced menu

            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.SaveLoaded += (_, _) => menus.CloseAll();
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;

            // v1.1 wiring (one region per feature; see architecture.md §16)

            // BEGIN SLOTS entry
            extensions = new ExtensionRegistry(menus);
            // END SLOTS entry

            // BEGIN THEME entry
            ThemeSwitcher.Register(helper.Events.Content);
            // END THEME entry

            // BEGIN HUD entry
            var layouts = new WindowLayoutStore(helper.Data);
            var hud = new HudService(helper, menus);
            UIServices.Layouts = layouts;
            UIServices.Hud = hud;
            helper.Events.GameLoop.SaveLoaded += (_, _) =>
            {
                // raised for each local split-screen player too: only the host reads (and later writes) the save's layouts
                if (Context.IsMainPlayer)
                {
                    layouts.Load();
                    hud.ApplyLayouts();
                }
            };
            helper.Events.GameLoop.Saving += (_, _) => layouts.Save();
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => layouts.Clear();
            // END HUD entry

            // BEGIN DATA entry
            UIServices.Hooks = new HookRegistry();
            DataService data = new(helper, Monitor, contexts, menus, hotkeys, composites, extensions);
            UIServices.Data = data;
            helper.Events.Content.AssetRequested += data.OnAssetRequested;
            helper.Events.Content.AssetsInvalidated += data.OnAssetsInvalidated;
            helper.Events.Content.AssetsInvalidated += (_, e) => TextureCache.Invalidate(e.NamesWithoutLocale);
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
            new DebugConsole(menus, extensions, composites, Monitor, data, Path.Combine(helper.DirectoryPath, "schema")).Register(helper.ConsoleCommands);
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

            ConfigMenu.RegisterWhenReady(Helper, ModManifest);
            ScreenReader.Connect(Helper, Monitor);
        }
    }
}
