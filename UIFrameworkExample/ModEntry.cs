using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using UIFramework.Api;

namespace UIFrameworkExample
{
    /// <summary>
    /// Example consumer of the UI Framework. It compiles against the copied <c>Api/IStardewUIApi.cs</c> only
    /// (no reference to the framework assembly): it builds the demo menu (<see cref="DemoMenu"/>, F9), the item image
    /// screen (<see cref="ItemImageDemo"/>, F7) and registers what data UIs can use from C#.
    /// </summary>
    public class ModEntry : Mod
    {
        /// <summary>C# composites the example exposes to data packs (v1.6): the custom gauge and the hand-drawn frame.</summary>
        private const string VolumeGaugeName = "6135.UIFrameworkExample.VolumeGauge";
        private const string FrameBoxName = "6135.UIFrameworkExample.FrameBox";

        private DemoMenu? demo;
        private ItemImageDemo? itemDemo;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            // this mod's own command prefix: SMAPI refuses duplicate command names, so don't use another mod's (like the framework's ui_)
            helper.ConsoleCommands.Add("uiex_demo", "Open the UI Framework example menu (load a save first).", (_, _) => OpenFromConsole(demo?.Menu));
            helper.ConsoleCommands.Add("uiex_items", "Open the item image (v1.2) demo menu (load a save first).", (_, _) => OpenFromConsole(itemDemo?.Menu));
            helper.ConsoleCommands.Add("uiex_hud", "Toggle the UI Framework example HUD widget.", (_, _) =>
            {
                if (demo != null)
                {
                    demo.Hud.Visible = !demo.Hud.Visible;
                }
            });
        }

        /// <summary>Open <paramref name="menu"/> in the world, like its hotkey does (never over the title screen, a dialogue or a shop).</summary>
        private void OpenFromConsole(IUIMenu? menu)
        {
            if (!Context.IsWorldReady)
            {
                Monitor.Log("Load a save first: the example menus open in the world.", LogLevel.Info);
                return;
            }
            menu?.Open(false);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            IStardewUIApi? ui = Helper.ModRegistry.GetApi<IStardewUIApi>("6135.UIFramework");
            if (ui == null)
            {
                Monitor.Log("UI Framework (6135.UIFramework) is not installed; the example menu is unavailable.", LogLevel.Warn);
                return;
            }
            Monitor.Log($"UI Framework API version {ui.ApiVersion}.", LogLevel.Info);

            demo = new DemoMenu(ui, Helper.Translation, Monitor, ModManifest.UniqueID);
            ui.BindToggleHotkey(demo.Menu, "F9");

            itemDemo = new ItemImageDemo(ui, Helper.Translation);
            ui.BindToggleHotkey(itemDemo.Menu, "F7");

            RegisterDataHooks(ui, demo);
        }

        /// <summary>
        /// The C# bridge (v1.6): what this mod offers to data UIs, e.g. the "[CP] UI Framework Example" pack's hybrid
        /// menu. Two C# composites (usable as custom tags: <c>{ "Type": "6135.UIFrameworkExample.VolumeGauge", "Value": "menu.volume" }</c>),
        /// a command (<c>"OnClick": "@6135.UIFrameworkExample/greet"</c>), a draw hook (<c>"DrawExtra": "6135.UIFrameworkExample/sparkle"</c>)
        /// and the settings object as a model (<c>model[6135.UIFrameworkExample/settings].FarmName</c>, or a Form's <c>Model</c>).
        /// Commands, hooks and model reads run in the calling player's screen, so per-screen state is read with <c>.Value</c>.
        /// </summary>
        private void RegisterDataHooks(IStardewUIApi api, DemoMenu menu)
        {
            // the gauge: "Value" is a data reference (menu.volume, config.x, model...), read and written by the component;
            // C# callers pass the setter under "Value.set"
            api.DefineComposite(VolumeGaugeName, (host, args) =>
            {
                double fallback = 50;
                Func<double> get = args.GetNumberGetter("Value") ?? (() => fallback);
                Action<double> set = args.GetNumberSetter("Value") ?? args.GetNumberSetter("Value.set") ?? (v => fallback = v);
                IUIElement gauge = api.AddCustom(host, host.Id + ".gauge", new VolumeGauge(get, set));
                if (args.Has("Hint"))
                {
                    gauge.Tooltip = args.GetGetter("Hint");
                }

                host.ExposeNumber("value", get);
                host.ExposeCommand("reset", () => set(0));
            });

            // the frame: the data element's Children go into the custom component's host (the default ContentTarget)
            api.DefineComposite(FrameBoxName, (host, args) =>
            {
                int padding = args.Has("Padding") ? (int)args.GetNumber("Padding") : 16;
                api.AddCustom(host, host.Id + ".frame", new FrameBox(), content => content.SetMargin(padding));
            });

            // a command: arguments arrive interpolated; it can read and write the caller's state
            api.RegisterCommand("greet", call =>
            {
                string who = call.Args.Length > 0 ? string.Join(" ", call.Args) : menu.Settings.FarmName;
                int greetings = ++menu.State.Greetings;
                call.SetState("menu.greeting", $"Hello {who}! (C# was called {greetings} time(s))");
                api.ShowToast($"Hello from C#, {who}!", 2000);
                Monitor.Log($"Command 'greet' from {call.OwnerModId} ({call.Menu?.Id ?? "no menu"}): {string.Join(" ", call.Args)}", LogLevel.Debug);
            });

            // a draw hook: a heart in the element's top-right corner, pulsing unless the player turned on reduced motion
            api.RegisterDrawHook("sparkle", (b, bounds, call) =>
            {
                float pulse = api.ReducedMotion ? 1f : 0.6f + (0.4f * (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0));
                b.Draw(Game1.mouseCursors, new Microsoft.Xna.Framework.Vector2(bounds.Right - 12, bounds.Y - 8), new Microsoft.Xna.Framework.Rectangle(211, 428, 7, 6),
                    Microsoft.Xna.Framework.Color.White * pulse, 0f, Microsoft.Xna.Framework.Vector2.Zero, 3f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 1f);
            });

            // the POCO the C# form edits, readable (and bindable) from data; resolved at each read, so each screen gets its own
            api.ExposeModelSource("settings", () => menu.Settings);
        }
    }
}
