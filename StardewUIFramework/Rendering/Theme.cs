using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>Mutable style bag handed to consumers through <see cref="IUIStyle"/>.</summary>
    internal sealed class UIStyle : IUIStyle
    {
        public UIFont? Font { get; set; }
        public Color? TextColor { get; set; }
        public Color? HoverColor { get; set; }
        internal Texture2D? BoxTexture { get; set; }
        public Rectangle? BoxSource { get; set; }
        public float? BoxScale { get; set; }
        public int? Padding { get; set; }
        public bool? TextShadow { get; set; }
        internal string? ClickSound { get; set; }
        internal string? HoverSound { get; set; }

        Texture2D IUIStyle.BoxTexture { get => BoxTexture!; set => BoxTexture = value; }
        string IUIStyle.ClickSound { get => ClickSound!; set => ClickSound = value; }
        string IUIStyle.HoverSound { get => HoverSound!; set => HoverSound = value; }
    }

    /// <summary>
    /// A fully resolved style: theme defaults, then the consumer's default style, then the element's own style.
    /// Box members stay nullable because each component has its own vanilla box (buttons vs panels vs text boxes).
    /// </summary>
    internal readonly record struct ResolvedStyle
    {
        internal readonly UIFont Font;
        internal readonly Color TextColor;
        /// <summary>Text color for disabled elements: the theme's when the text color is not overridden, else the text color at half opacity.</summary>
        internal readonly Color DisabledTextColor;
        internal readonly Color HoverColor;
        internal readonly Texture2D? BoxTexture;
        internal readonly Rectangle? BoxSource;
        internal readonly float? BoxScale;
        internal readonly int? Padding;
        internal readonly bool TextShadow;
        /// <summary>null = component default, empty = silent.</summary>
        internal readonly string? ClickSound;
        /// <summary>null = component default, empty = silent.</summary>
        internal readonly string? HoverSound;

        /// <summary>Resolve each member from the first layer that sets it: <paramref name="own"/>, then <paramref name="consumer"/>, then <paramref name="theme"/> (no allocation).</summary>
        internal ResolvedStyle(UIStyle theme, UIStyle? consumer, UIStyle? own)
        {
            Color? text = own?.TextColor ?? consumer?.TextColor ?? theme.TextColor;
            Font = own?.Font ?? consumer?.Font ?? theme.Font ?? UIFont.Small;
            TextColor = text ?? Theme.TextColor;
            DisabledTextColor = text.HasValue ? text.Value * 0.5f : Theme.DisabledTextColor;
            HoverColor = own?.HoverColor ?? consumer?.HoverColor ?? theme.HoverColor ?? Theme.HoverColor;
            BoxTexture = own?.BoxTexture ?? consumer?.BoxTexture ?? theme.BoxTexture;
            BoxSource = own?.BoxSource ?? consumer?.BoxSource ?? theme.BoxSource;
            BoxScale = own?.BoxScale ?? consumer?.BoxScale ?? theme.BoxScale;
            Padding = own?.Padding ?? consumer?.Padding ?? theme.Padding;
            TextShadow = own?.TextShadow ?? consumer?.TextShadow ?? theme.TextShadow ?? false;
            ClickSound = own?.ClickSound ?? consumer?.ClickSound ?? theme.ClickSound;
            HoverSound = own?.HoverSound ?? consumer?.HoverSound ?? theme.HoverSound;
        }
    }

    /// <summary>
    /// The bottom style layer: vanilla sprite regions and sounds, overridden by the active <see cref="ThemeData"/>
    /// from the <see cref="AssetName"/> asset (selected by <see cref="ModConfig.Theme"/>). Per-element
    /// <see cref="UIStyle"/> overrides sit on top exactly as before; the theme only changes what nobody overrode.
    /// </summary>
    internal static class Theme
    {
        /// <summary>The Content Patcher-patchable data asset holding every theme.</summary>
        internal const string AssetName = "Mods/6135.UIFramework/Themes";

        /// <summary>Name of the vanilla theme.</summary>
        internal const string DefaultThemeName = "default";

        private const float MinFontScale = 0.25f;
        private const float MaxFontScale = 4f;

        // vanilla 9-slice boxes and sprites
        private static readonly Rectangle VanillaPanelBoxSource = new(0, 256, 60, 60);      // Game1.menuTexture
        private static readonly Rectangle VanillaButtonBoxSource = new(432, 439, 9, 9);     // Game1.mouseCursors
        internal static readonly Rectangle DropdownBoxSource = new(433, 451, 3, 3);     // OptionsDropDown.dropDownBGSource
        internal static readonly Rectangle DropdownButtonSource = new(437, 450, 10, 11); // OptionsDropDown.dropDownButtonSource
        internal static readonly Rectangle CheckboxChecked = new(236, 425, 9, 9);       // OptionsCheckbox.sourceRectChecked
        internal static readonly Rectangle CheckboxUnchecked = new(227, 425, 9, 9);     // OptionsCheckbox.sourceRectUnchecked
        internal static readonly Rectangle ScrollUpArrow = new(421, 459, 11, 12);
        internal static readonly Rectangle ScrollDownArrow = new(421, 472, 11, 12);
        internal static readonly Rectangle ScrollTrack = new(403, 383, 6, 6);
        internal static readonly Rectangle ScrollThumb = new(435, 463, 6, 10);
        internal static readonly Rectangle SliderTrack = new(403, 383, 6, 6);
        internal static readonly Rectangle SliderKnob = new(420, 441, 10, 6);           // OptionsSlider.sliderButtonRect

        // vanilla sounds (the active theme may replace the click / hover / open / close ones)
        internal const string ScrollSound = "shiny4";
        internal const string BackspaceSound = "tinyWhip";
        internal const string TypeSound = "cowboy_monsterhit";

        internal const int PixelScale = 4;

        private static Dictionary<string, ThemeData>? themes;
        private static ResolvedTheme? resolved;
        private static string? warnedMissing;
        private static string? textureRequester;

        /// <summary>
        /// Changes whenever what the theme resolves to may have changed (asset invalidated, another theme selected, text
        /// scale or config changed). Menus and HUD widgets lay out again when it differs from the version of their last layout.
        /// </summary>
        internal static int Version { get; private set; }

        // ---------------------------------------------------------------------------------------------------------
        //  Active theme
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The active theme's raw data.</summary>
        internal static ThemeData Active => Current.Data;

        /// <summary>Name of the active theme (the configured name when it exists, else <see cref="DefaultThemeName"/>).</summary>
        internal static string ActiveName => Current.Name;

        /// <summary>Names of every theme in the asset (at least <see cref="DefaultThemeName"/>).</summary>
        internal static string[] ThemeNames => LoadThemes().Keys.ToArray();

        /// <summary>Forget the loaded asset, textures and derived values so the next access reloads them (asset invalidated / config changed).</summary>
        internal static void Invalidate()
        {
            themes = null;
            resolved = null;
            textureRequester = null;
            TextureCache.Invalidate();
            warnedMissing = null;
            Version++;
        }

        /// <summary>The parsed active theme, re-resolved when the configured name changes or the asset was invalidated.</summary>
        private static ResolvedTheme Current
        {
            get
            {
                string wanted = UIServices.Config.Theme ?? DefaultThemeName;
                if (resolved == null || !string.Equals(resolved.Requested, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    if (resolved != null)
                    {
                        Version++; // another theme was configured without an Invalidate (config reset)
                    }

                    resolved = Resolve(wanted);
                    textureRequester = null;
                }

                return resolved;
            }
        }

        private static ResolvedTheme Resolve(string requested)
        {
            Dictionary<string, ThemeData> all = LoadThemes();
            string name = all.Keys.FirstOrDefault(k => string.Equals(k, requested, StringComparison.OrdinalIgnoreCase)) ?? DefaultThemeName;
            if (!string.Equals(name, requested, StringComparison.OrdinalIgnoreCase) && warnedMissing != requested)
            {
                warnedMissing = requested;
                UIServices.Log($"Theme '{requested}' is not defined in {AssetName}; using '{name}'.", LogLevel.Warn);
            }

            return new ResolvedTheme(requested, name, all.TryGetValue(name, out ThemeData? data) ? data : new ThemeData());
        }

        /// <summary>Load the asset through the content pipeline (so Content Patcher edits apply); the vanilla theme alone when that fails.</summary>
        private static Dictionary<string, ThemeData> LoadThemes()
        {
            if (themes != null)
            {
                return themes;
            }

            Dictionary<string, ThemeData>? loaded = null;
            try
            {
                loaded = UIServices.GameContent?.Load<Dictionary<string, ThemeData>>(AssetName);
            }
            catch (Exception ex)
            {
                UIServices.Log($"Could not load the theme asset {AssetName}; using the vanilla theme.\n{ex}", LogLevel.Warn);
            }

            var result = new Dictionary<string, ThemeData>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ThemeData> pair in loaded ?? new Dictionary<string, ThemeData>())
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                {
                    result[pair.Key] = pair.Value;
                }
            }
            if (!result.ContainsKey(DefaultThemeName))
            {
                result[DefaultThemeName] = new ThemeData();
            }

            themes = result;
            return result;
        }

        /// <summary>Load a texture asset by name through the shared <see cref="TextureCache"/>; null when the name is empty or the load fails.</summary>
        private static Texture2D? LoadTexture(string? assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            // the log requester is built once per resolved theme, not on every read
            return TextureCache.Load(assetName, textureRequester ??= $"Theme '{ActiveName}'");
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Colors
        // ---------------------------------------------------------------------------------------------------------

        internal static Color TextColor => Current.TextColor ?? Game1.textColor;

        internal static Color DisabledTextColor => Current.DisabledTextColor ?? TextColor * 0.5f;

        internal static Color HoverColor => Current.HoverColor ?? Color.Wheat;

        internal static Color ScrollbarTint => Current.ScrollbarTint ?? Color.White;

        /// <summary>Tint multiplied into every themed box texture (white = untinted).</summary>
        internal static Color BoxTint => Current.BoxTint ?? Color.White;

        /// <summary>Solid fill color for boxes, or null to draw box textures.</summary>
        internal static Color? BoxFill => Current.BoxFill;

        internal static Color BorderColor => Current.BorderColor ?? TextColor;

        internal static int BorderThickness => Math.Max(1, Active.BorderThickness);

        /// <summary>True when the theme leaves the box chrome untouched (menus then use the vanilla dialogue box).</summary>
        internal static bool IsVanillaChrome => Current.BoxFill == null && Current.BoxTint == null && Current.PanelTexture == null;

        /// <summary>Tint for a box / sprite: gray when disabled, the hover color when hovered or focused, white otherwise.</summary>
        internal static Color StateTint(bool enabled, bool highlighted, Color hoverColor)
        {
            if (!enabled)
            {
                return Color.Gray;
            }

            return highlighted ? hoverColor : Color.White;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Textures / sprite regions
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Panel 9-slice texture (theme override or <c>Game1.menuTexture</c>).</summary>
        internal static Texture2D PanelTexture => Current.PanelTexture ?? Game1.menuTexture;

        /// <summary>3x3 tile region of <see cref="PanelTexture"/>.</summary>
        internal static Rectangle PanelBoxSource => Current.PanelSource ?? (Current.PanelTexture?.Bounds ?? VanillaPanelBoxSource);

        /// <summary>Button 9-slice texture (theme override or <c>Game1.mouseCursors</c>).</summary>
        internal static Texture2D ButtonTexture => Current.ButtonTexture ?? Game1.mouseCursors;

        /// <summary>3x3 tile region of <see cref="ButtonTexture"/>.</summary>
        internal static Rectangle ButtonBoxSource => Current.ButtonSource ?? (Current.ButtonTexture?.Bounds ?? VanillaButtonBoxSource);

        /// <summary>Text box texture (theme override or the vanilla <c>LooseSprites\textBox</c>).</summary>
        internal static Texture2D TextBoxTexture => Current.TextBoxTexture ?? UIServices.TextBoxTexture;

        // ---------------------------------------------------------------------------------------------------------
        //  Sounds
        // ---------------------------------------------------------------------------------------------------------

        internal static string ButtonClickSound => Active.ClickSound ?? "select";
        internal static string CheckboxClickSound => Active.ClickSound ?? "drumkit6";
        internal static string DropdownOpenSound => Active.OpenSound ?? "shwip";
        internal static string DropdownCloseSound => Active.CloseSound ?? "drumkit6";
        internal static string HoverSound => Active.HoverSound ?? string.Empty;

        // ---------------------------------------------------------------------------------------------------------
        //  Scales / accessibility
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Text scale: the theme's font scale times the player's text scale setting.</summary>
        internal static float FontScale => Math.Clamp(Active.FontScale * UIServices.Config.TextScale, MinFontScale, MaxFontScale);

        /// <summary>Scale a spacing / padding value by the theme's spacing scale.</summary>
        internal static int Space(int pixels)
        {
            float scale = Active.SpacingScale;
            if (pixels <= 0 || scale <= 0 || Numbers.Same(scale, 1f))
            {
                return pixels;
            }

            return (int)Math.Round(pixels * scale);
        }

        /// <summary>A consumer-given row height (or text box width) grown with the text scale so scaled text still fits; never smaller than the given value.</summary>
        internal static int ScaleForText(int pixels) => (int)Math.Round(pixels * Math.Max(1f, FontScale));

        /// <summary>Extra height a fixed-size row needs so scaled text of <paramref name="font"/> still fits (0 at scale 1 or below).</summary>
        internal static int ExtraTextHeight(UIFont font)
        {
            float scale = FontScale;
            if (scale <= 1f)
            {
                return 0;
            }

            float scaled = UIServices.Text.LineHeight(font);
            return (int)Math.Ceiling(scaled - (scaled / scale));
        }

        /// <summary>Whether the player asked for no animation (the caret stays solid instead of blinking).</summary>
        internal static bool ReducedMotion => UIServices.Config.ReducedMotion;

        /// <summary>The theme's base style (what per-consumer / per-element styles override).</summary>
        internal static UIStyle Default => Current.Style;

        /// <summary>The active theme with every string member parsed once.</summary>
        private sealed class ResolvedTheme
        {
            internal readonly string Requested;
            internal readonly string Name;
            internal readonly ThemeData Data;
            internal readonly Color? TextColor;
            internal readonly Color? DisabledTextColor;
            internal readonly Color? HoverColor;
            internal readonly Color? ScrollbarTint;
            internal readonly Color? BoxTint;
            internal readonly Color? BoxFill;
            internal readonly Color? BorderColor;
            internal readonly Rectangle? PanelSource;
            internal readonly Rectangle? ButtonSource;
            internal readonly UIStyle Style;

            internal ResolvedTheme(string requested, string name, ThemeData data)
            {
                Requested = requested;
                Name = name;
                Data = data;
                TextColor = ThemeData.ParseColor(data.TextColor);
                DisabledTextColor = ThemeData.ParseColor(data.DisabledTextColor);
                HoverColor = ThemeData.ParseColor(data.HoverColor);
                ScrollbarTint = ThemeData.ParseColor(data.ScrollbarTint);
                BoxTint = ThemeData.ParseColor(data.BoxTint);
                BoxFill = ThemeData.ParseColor(data.BoxFill);
                BorderColor = ThemeData.ParseColor(data.BorderColor);
                PanelSource = ThemeData.ParseRectangle(data.BoxSource);
                ButtonSource = ThemeData.ParseRectangle(data.ButtonSource);
                Style = new UIStyle
                {
                    Font = UIFont.Small,
                    TextColor = null, // resolved lazily so Game1.textColor is read at draw time
                    TextShadow = data.ShadowEnabled
                };
            }

            // textures load lazily (the content pipeline may not be ready when the theme resolves) and are cached by name
            internal Texture2D? PanelTexture => LoadTexture(Data.BoxTexture);
            internal Texture2D? ButtonTexture => LoadTexture(Data.ButtonTexture);
            internal Texture2D? TextBoxTexture => LoadTexture(Data.TextBoxTexture);
        }
    }
}
