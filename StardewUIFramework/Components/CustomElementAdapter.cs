using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// Wraps a consumer-implemented <see cref="IUICustomComponent"/> (architecture.md §7, tier 2) so it takes part in
    /// layout, hit-testing, focus, tooltips and event bubbling like a built-in element. The calls into the
    /// implementation go through <see cref="CustomComponentBridge"/>, which guards each one.
    /// </summary>
    internal sealed class CustomElementAdapter : UIElement
    {
        private readonly CustomComponentBridge bridge;

        internal CustomElementAdapter(string id, IUICustomComponent implementation) : base(id)
        {
            bridge = new CustomComponentBridge(this, implementation);
        }

        internal override bool Focusable => bridge.WantsFocus;

        /// <summary>Custom components have no text of their own: the consumer's <c>AccessibleName</c> or the tooltip describes them.</summary>
        internal override string AccessibleDescription
        {
            get
            {
                string? tooltip = Tooltip == null ? null : Raise("Tooltip", Tooltip, string.Empty);
                return Accessibility.Compose(Accessibility.Text("custom", "Custom"), tooltip ?? Id);
            }
        }

        /// <summary>Overlay components draw (content + <c>OnDrawExtra</c>) and are hit-tested above the tree.</summary>
        protected override bool DrawsInOverlay => bridge.WantsOverlay;

        protected override Vector2 MeasureCore(Vector2 available) => bridge.Measure(available);

        protected override float MinWidthCore() => bridge.MinimumWidth;

        protected override void DrawCore(SpriteBatch b) => bridge.Draw(b, Bounds);

        internal override void Update(double elapsedMs) => bridge.Update(Bounds, elapsedMs);

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (bridge.Click(e))
            {
                e.Handled = true;
            }
            return base.HandleClick(e);
        }

        protected internal override void HandleHoverMove(int px, int py) => bridge.HoverMove(px, py);

        protected internal override void HandleHoverLeave()
        {
            bridge.HoverLeave();
            base.HandleHoverLeave();
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (bridge.Key(e))
            {
                e.Handled = true;
            }
            return base.HandleKey(e);
        }
    }
}
