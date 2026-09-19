using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using UIFramework;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Hosting;

namespace StardewUIFramework.Tests.Testing
{
    /// <summary>
    /// Runs the framework without the game: installs the fakes into <see cref="UIServices"/> (monospace text
    /// measurement, a sound recorder, a fixed viewport, a manual clock and an in-memory keyboard subscriber) and
    /// creates a consumer context, a menu registry and a <see cref="StardewUIApi"/> exactly as <c>ModEntry</c> would.
    /// Menus built through <see cref="Api"/> are laid out and driven with <see cref="Drive"/>; they are never opened as
    /// <c>IClickableMenu</c>s (that needs <c>Game1.player</c>).
    /// <para>Tests share the static <see cref="UIServices"/>, so the test assembly disables xUnit parallelization.</para>
    /// </summary>
    public sealed class TestHost
    {
        /// <summary>The mod id the fake consumer registers under.</summary>
        public const string ConsumerId = "test.consumer";

        private readonly List<string> sounds = new();

        public TestHost()
        {
            Text = new FakeTextMeasurer();
            Config = new ModConfig();
            UIServices.Text = Text;
            UIServices.Config = Config;
            UIServices.Monitor = null;
            UIServices.PlaySound = cue =>
            {
                if (!string.IsNullOrEmpty(cue))
                {
                    sounds.Add(cue);
                }
            };
            UIServices.ViewportSize = () => Viewport;
            UIServices.NowMs = () => NowMs;
            FocusManager.GetSubscriber = () => Subscriber;
            FocusManager.SetSubscriber = s => Subscriber = s;
            PerfCounters.Enabled = false;

            Consumer = new ConsumerContext(ConsumerId);
            Registry = new MenuRegistry();
            Hotkeys = new HotkeyService();
            Api = new StardewUIApi(Consumer, Registry, Hotkeys);
        }

        /// <summary>The monospace measurer installed in <see cref="UIServices.Text"/>.</summary>
        public FakeTextMeasurer Text { get; }

        /// <summary>The framework config in effect (tooltip delay etc.).</summary>
        public ModConfig Config { get; }

        /// <summary>Sound cues played so far, in order.</summary>
        public IReadOnlyList<string> Sounds => sounds;

        /// <summary>UI viewport size reported to the layout (default 1280x720).</summary>
        public Point Viewport { get; set; } = new(1280, 720);

        /// <summary>Manual clock read by carets, tooltips and hover timing; advance it with <see cref="Advance"/>.</summary>
        public double NowMs { get; set; }

        /// <summary>Stand-in for <c>Game1.keyboardDispatcher.Subscriber</c> (only touched by open menus).</summary>
        public IKeyboardSubscriber? Subscriber { get; set; }

        /// <summary>The per-consumer API facade, as <c>helper.ModRegistry.GetApi</c> would hand it out.</summary>
        public StardewUIApi Api { get; }

        internal ConsumerContext Consumer { get; }

        internal MenuRegistry Registry { get; }

        internal HotkeyService Hotkeys { get; }

        /// <summary>Move the clock forward.</summary>
        public void Advance(double ms) => NowMs += ms;

        /// <summary>Forget the recorded sounds.</summary>
        public void ClearSounds() => sounds.Clear();

        /// <summary>Create a chrome-less menu (no box, no padding) so element bounds are easy to predict.</summary>
        public IUIMenu CreateBareMenu(string id)
        {
            IUIMenuOptions options = Api.CreateMenuOptions();
            options.DrawBox = false;
            options.ShowCloseButton = false;
            return Api.CreateMenu(id, options);
        }

        /// <summary>Run a layout pass on the menu now.</summary>
        public static void Layout(IUIMenu menu) => Unwrap(menu).Relayout();

        /// <summary>An input driver for the menu. The menu is laid out first so element bounds are valid right away.</summary>
        public InputDriver Drive(IUIMenu menu)
        {
            Layout(menu);
            return new InputDriver(this, Unwrap(menu));
        }

        internal static UIMenu Unwrap(IUIMenu menu) => (UIMenu)menu;
    }
}
