using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;

namespace UIFramework.Rendering
{
    /// <summary>Mutable style bag handed to consumers through <see cref="IUIStyle"/>.</summary>
    internal sealed class UIStyle : IUIStyle
    {
        public UIFont? Font { get; set; }
        public Color? TextColor { get; set; }
        public Color? HoverColor { get; set; }
        public Texture2D? BoxTexture { get; set; }
        public Rectangle? BoxSource { get; set; }
        public float? BoxScale { get; set; }
        public int? Padding { get; set; }
        public bool? TextShadow { get; set; }
        public string? ClickSound { get; set; }
        public string? HoverSound { get; set; }

        Texture2D IUIStyle.BoxTexture { get => BoxTexture!; set => BoxTexture = value; }
        string IUIStyle.ClickSound { get => ClickSound!; set => ClickSound = value; }
        string IUIStyle.HoverSound { get => HoverSound!; set => HoverSound = value; }

        /// <summary>Copy every member set on <paramref name="over"/> onto a clone of this style.</summary>
        public UIStyle Merge(UIStyle? over)
        {
            var result = new UIStyle
            {
                Font = Font, TextColor = TextColor, HoverColor = HoverColor, BoxTexture = BoxTexture, BoxSource = BoxSource,
                BoxScale = BoxScale, Padding = Padding, TextShadow = TextShadow, ClickSound = ClickSound, HoverSound = HoverSound
            };
            if (over == null)
                return result;
            result.Font = over.Font ?? result.Font;
            result.TextColor = over.TextColor ?? result.TextColor;
            result.HoverColor = over.HoverColor ?? result.HoverColor;
            result.BoxTexture = over.BoxTexture ?? result.BoxTexture;
            result.BoxSource = over.BoxSource ?? result.BoxSource;
            result.BoxScale = over.BoxScale ?? result.BoxScale;
            result.Padding = over.Padding ?? result.Padding;
            result.TextShadow = over.TextShadow ?? result.TextShadow;
            result.ClickSound = over.ClickSound ?? result.ClickSound;
            result.HoverSound = over.HoverSound ?? result.HoverSound;
            return result;
        }
    }

    /// <summary>
    /// A fully resolved style: theme defaults, then the consumer's default style, then the element's own style.
    /// Box members stay nullable because each component has its own vanilla box (buttons vs panels vs text boxes).
    /// </summary>
    internal readonly struct ResolvedStyle
    {
        public readonly UIFont Font;
        public readonly Color TextColor;
        public readonly Color HoverColor;
        public readonly Texture2D? BoxTexture;
        public readonly Rectangle? BoxSource;
        public readonly float? BoxScale;
        public readonly int? Padding;
        public readonly bool TextShadow;
        /// <summary>null = component default, empty = silent.</summary>
        public readonly string? ClickSound;
        /// <summary>null = component default, empty = silent.</summary>
        public readonly string? HoverSound;

        public ResolvedStyle(UIStyle s)
        {
            Font = s.Font ?? UIFont.Small;
            TextColor = s.TextColor ?? Theme.TextColor;
            HoverColor = s.HoverColor ?? Color.Wheat;
            BoxTexture = s.BoxTexture;
            BoxSource = s.BoxSource;
            BoxScale = s.BoxScale;
            Padding = s.Padding;
            TextShadow = s.TextShadow ?? false;
            ClickSound = s.ClickSound;
            HoverSound = s.HoverSound;
        }
    }

    /// <summary>Vanilla look: textures, source rectangles and sounds used by default.</summary>
    internal static class Theme
    {
        // 9-slice boxes
        public static readonly Rectangle PanelBoxSource = new(0, 256, 60, 60);        // Game1.menuTexture
        public static readonly Rectangle ButtonBoxSource = new(432, 439, 9, 9);       // Game1.mouseCursors
        public static readonly Rectangle DropdownBoxSource = new(433, 451, 3, 3);     // OptionsDropDown.dropDownBGSource
        public static readonly Rectangle DropdownButtonSource = new(437, 450, 10, 11); // OptionsDropDown.dropDownButtonSource
        public static readonly Rectangle CheckboxChecked = new(236, 425, 9, 9);       // OptionsCheckbox.sourceRectChecked
        public static readonly Rectangle CheckboxUnchecked = new(227, 425, 9, 9);     // OptionsCheckbox.sourceRectUnchecked
        public static readonly Rectangle ScrollUpArrow = new(421, 459, 11, 12);
        public static readonly Rectangle ScrollDownArrow = new(421, 472, 11, 12);
        public static readonly Rectangle ScrollTrack = new(403, 383, 6, 6);
        public static readonly Rectangle ScrollThumb = new(435, 463, 6, 10);
        public static readonly Rectangle SliderTrack = new(403, 383, 6, 6);
        public static readonly Rectangle SliderKnob = new(420, 441, 10, 6);           // OptionsSlider.sliderButtonRect

        // sounds
        public const string ButtonClickSound = "select";
        public const string CheckboxClickSound = "drumkit6";
        public const string DropdownOpenSound = "shwip";
        public const string DropdownCloseSound = "drumkit6";
        public const string ScrollSound = "shiny4";
        public const string BackspaceSound = "tinyWhip";
        public const string TypeSound = "cowboy_monsterhit";
        public const string HoverSound = "";

        public const int PixelScale = 4;

        public static Color TextColor => Game1.textColor;

        /// <summary>The theme's base style (every member set).</summary>
        public static UIStyle Default { get; } = new()
        {
            Font = UIFont.Small,
            TextColor = null, // resolved lazily so Game1.textColor is read at draw time
            HoverColor = Color.Wheat,
            TextShadow = false,
            Padding = null,
            ClickSound = null,
            HoverSound = null
        };
    }
}
