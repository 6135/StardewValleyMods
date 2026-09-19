using System;
using UIFramework.Api;

namespace UIFramework.Api
{
    /// <summary>Plain bag of initial menu settings (see <see cref="IUIMenuOptions"/>).</summary>
    internal sealed class UIMenuOptions : IUIMenuOptions
    {
        internal Func<string>? TitleFunc { get; set; }
        Func<string> IUIMenuOptions.Title { get => TitleFunc!; set => TitleFunc = value; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public bool ShowCloseButton { get; set; } = true;
        public bool Modal { get; set; } = true;
        public bool DimBackground { get; set; } = true;
        public UIAnchor Anchor { get; set; } = UIAnchor.Center;
        public int X { get; set; }
        public int Y { get; set; }
        public bool DrawBox { get; set; } = true;
        public int Padding { get; set; }
        public bool CloseOnEscape { get; set; } = true;

        // HUD
        public bool PlayerLayout { get; set; } = true;
    }
}
