using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// Wraps a consumer-implemented <see cref="IUICustomComponent"/> (architecture.md §7, tier 2) so it takes part in
    /// layout, hit-testing, focus, tooltips and event bubbling like a built-in element. Every call into the
    /// implementation crosses the API proxy and is guarded by <see cref="UIElement.Raise(string, System.Action?)"/>,
    /// so a faulting component is logged and muted instead of crashing the game loop. The implementation's calls use
    /// their own <c>Custom.*</c> event names so muting one never silences the element's public
    /// <see cref="IUIElement.OnClick"/> / <see cref="IUIElement.OnHover"/> / <see cref="IUIElement.OnKey"/> callbacks
    /// (or the reverse).
    /// </summary>
    internal sealed class CustomElementAdapter : UIElement
    {
        private readonly IUICustomComponent implementation;
        private Point lastHover;

        internal CustomElementAdapter(string id, IUICustomComponent implementation) : base(id)
        {
            this.implementation = implementation;
        }

        internal override bool Focusable => Raise("Custom.WantsFocus", () => implementation.WantsFocus, false);

        /// <summary>Custom components have no text of their own: the consumer's <c>AccessibleName</c> or the tooltip describes them.</summary>
        internal override string AccessibleDescription
        {
            get
            {
                string? tooltip = Tooltip == null ? null : Raise("Tooltip", Tooltip, string.Empty);
                return Accessibility.Compose("Custom", tooltip ?? Id);
            }
        }

        /// <summary>Overlay components draw (content + <c>OnDrawExtra</c>) and are hit-tested above the tree.</summary>
        protected override bool DrawsInOverlay => Raise("Custom.WantsOverlay", () => implementation.WantsOverlay, false);

        protected override Vector2 MeasureCore(Vector2 available)
        {
            Vector2 size = Raise("Custom.Measure", () => implementation.Measure(available), Vector2.Zero);
            return new Vector2(System.Math.Max(0, size.X), System.Math.Max(0, size.Y));
        }

        protected override void DrawCore(SpriteBatch b)
        {
            Rectangle bounds = Bounds;
            Raise("Custom.Draw", () => implementation.Draw(b, bounds));
        }

        internal override void Update(double elapsedMs)
        {
            Rectangle bounds = Bounds;
            Raise("Custom.Update", () => implementation.Update(bounds, elapsedMs));
        }

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && Raise("Custom.OnClick", () => implementation.OnClick(e.X, e.Y, e.Button == UIMouseButton.Right), false))
            {
                e.Handled = true;
            }
            return base.HandleClick(e);
        }

        // the router raises Enter and then Move(x, y) in the same pass, so Enter needs no call of its own
        protected internal override void HandleHoverMove(int px, int py)
        {
            lastHover = new Point(px, py);
            Raise("Custom.OnHover", () => implementation.OnHover(px, py, true));
        }

        protected internal override void HandleHoverLeave()
        {
            Point at = lastHover;
            Raise("Custom.OnHoverEnd", () => implementation.OnHover(at.X, at.Y, false));
            base.HandleHoverLeave();
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (e.Target == this && Raise("Custom.OnKey", () => implementation.OnKey(e.Key, e.Shift, e.Ctrl), false))
            {
                e.Handled = true;
            }
            return base.HandleKey(e);
        }
    }
}
