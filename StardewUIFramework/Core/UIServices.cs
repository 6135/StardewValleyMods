using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Api;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>Measures text without exposing <see cref="SpriteFont"/>.</summary>
    internal interface ITextMeasurer
    {
        Vector2 Measure(UIFont font, string text, float scale);

        /// <summary>Wrap <paramref name="text"/> so no line exceeds <paramref name="width"/> pixels.</summary>
        string Wrap(UIFont font, string text, int width);

        /// <summary>
        /// Width of the widest piece <see cref="Wrap"/> never breaks (a word, or a single character in languages
        /// wrapped per character) at <paramref name="scale"/>: the narrowest width wrapped text fits in.
        /// </summary>
        float LongestWord(UIFont font, string text, float scale);

        float LineHeight(UIFont font);
    }

    /// <summary>Real implementation backed by the vanilla fonts.</summary>
    internal sealed class GameTextMeasurer : ITextMeasurer
    {
        // every result includes the theme / accessibility font scale so layout and drawing agree
        public Vector2 Measure(UIFont font, string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new Vector2(0, LineHeight(font) * scale);
            }

            return GetFont(font).MeasureString(text) * (scale * Theme.FontScale);
        }

        public string Wrap(UIFont font, string text, int width)
        {
            return Game1.parseText(text ?? string.Empty, GetFont(font), Math.Max(1, (int)(width / Theme.FontScale)));
        }

        // Game1.parseText breaks Japanese / Chinese / Thai between any two characters, other languages on spaces and line breaks
        public float LongestWord(UIFont font, string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            LocalizedContentManager.LanguageCode language = LocalizedContentManager.CurrentLanguageCode;
            bool perCharacter = language is LocalizedContentManager.LanguageCode.ja or LocalizedContentManager.LanguageCode.zh or LocalizedContentManager.LanguageCode.th;
            float widest = 0;
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                bool breakHere = i == text.Length || text[i] == ' ' || text[i] == '\n' || text[i] == '\r';
                if (!breakHere && !perCharacter)
                {
                    continue;
                }

                int end = breakHere ? i : i + 1;
                if (end > start)
                {
                    widest = Math.Max(widest, Measure(font, text.Substring(start, end - start), scale).X);
                }

                start = end + (breakHere ? 1 : 0);
            }

            return widest;
        }

        public float LineHeight(UIFont font) => GetFont(font).LineSpacing * Theme.FontScale;

        internal static SpriteFont GetFont(UIFont font) => font switch
        {
            UIFont.Dialogue => Game1.dialogueFont,
            UIFont.Tiny => Game1.tinyFont,
            _ => Game1.smallFont
        };
    }

    /// <summary>
    /// Process-wide services used by the element tree. Set once by <see cref="ModEntry"/> so the layout /
    /// routing code never touches <see cref="Game1"/> directly.
    /// </summary>
    internal static class UIServices
    {
        /// <summary>Text measurement.</summary>
        internal static ITextMeasurer Text { get; set; } = new GameTextMeasurer();

        /// <summary>Play a sound cue; null / empty cue plays nothing.</summary>
        internal static Action<string?> PlaySound { get; set; } = cue =>
        {
            if (!string.IsNullOrEmpty(cue))
            {
                Game1.playSound(cue); } };

        /// <summary>Size of the UI viewport in UI pixels.</summary>
        internal static Func<Point> ViewportSize { get; set; } = () => new Point(Game1.uiViewport.Width, Game1.uiViewport.Height);

        /// <summary>Monotonic clock in milliseconds (caret blink, tooltip delay).</summary>
        internal static Func<double> NowMs { get; set; } = () => Game1.currentGameTime?.TotalGameTime.TotalMilliseconds ?? Environment.TickCount64;

        /// <summary>Framework log.</summary>
        internal static IMonitor? Monitor { get; set; }

        /// <summary>Framework configuration.</summary>
        internal static ModConfig Config { get; set; } = new();

        /// <summary>Game content helper.</summary>
        internal static IGameContentHelper? GameContent { get; set; }

        /// <summary>Translations.</summary>
        internal static ITranslationHelper? Translation { get; set; }

        // HUD
        /// <summary>HUD widgets and toasts (null until <see cref="ModEntry"/> wired it).</summary>
        internal static Hosting.HudService? Hud { get; set; }

        // DATA
        /// <summary>Data-driven menus (null until <see cref="ModEntry"/> wired it).</summary>
        internal static global::UIFramework.Data.DataService? Data { get; set; }

        /// <summary>The C# bridge of data UIs: commands, functions, sources, draw hooks and exposed values (null until <see cref="ModEntry"/> wired it).</summary>
        internal static Hosting.HookRegistry? Hooks { get; set; }

        /// <summary>Player-owned window layouts for the current save (null until <see cref="ModEntry"/> wired it).</summary>
        internal static Hosting.WindowLayoutStore? Layouts { get; set; }

        /// <summary>Persists <see cref="Config"/> (set by <see cref="ModEntry"/>).</summary>
        internal static Action? SaveConfig { get; set; }

        /// <summary>Screen reader output (Stardew Access when installed); null when no screen reader mod is present.</summary>
        internal static Action<string>? Announcer { get; set; }

        private static Texture2D? textBoxTexture;

        /// <summary>The vanilla text box texture (<c>LooseSprites\textBox</c>).</summary>
        internal static Texture2D TextBoxTexture => textBoxTexture ??= GameContent?.Load<Texture2D>("LooseSprites\\textBox") ?? Game1.content.Load<Texture2D>("LooseSprites\\textBox");

        internal static void Log(string message, LogLevel level = LogLevel.Trace) => Monitor?.Log(message, level);
    }
}
