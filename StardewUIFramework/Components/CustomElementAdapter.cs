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
    /// so a faulting component is logged and muted instead of crashing the game loop.
    /// </summary>
    internal sealed class CustomElementAdapter : UIElement
    {
        private readonly IUICustomComponent implementation;
        private Point lastHover;

        internal CustomElementAdapter(string id, IUICustomComponent implementation) : base(id)
        {
            this.implementation = implementation;
        }

        internal override bool Focusable => Raise("WantsFocus", () => implementation.WantsFocus, false);

        protected override Vector2 MeasureCore(Vector2 available)
        {
            Vector2 size = Raise("Measure", () => implementation.Measure(available), Vector2.Zero);
            return new Vector2(System.Math.Max(0, size.X), System.Math.Max(0, size.Y));
        }

        protected override void DrawCore(SpriteBatch b)
        {
            Rectangle bounds = Bounds;
            if (OwnerMenu != null && Raise("WantsOverlay", () => implementation.WantsOverlay, false))
            {
                OwnerMenu.Overlay.RegisterDraw(sb => Raise("Draw", () => implementation.Draw(sb, bounds)));
                return;
            }
            Raise("Draw", () => implementation.Draw(b, bounds));
        }

        internal override void Update(double elapsedMs)
        {
            Rectangle bounds = Bounds;
            Raise("Update", () => implementation.Update(bounds, elapsedMs));
        }

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && Raise("OnClick", () => implementation.OnClick(e.X, e.Y, e.Button == UIMouseButton.Right), false))
            {
                e.Handled = true;
            }
            return base.HandleClick(e);
        }

        // the router raises Enter and then Move(x, y) in the same pass, so Enter needs no call of its own
        protected internal override void HandleHoverMove(int px, int py)
        {
            lastHover = new Point(px, py);
            Raise("OnHover", () => implementation.OnHover(px, py, true));
        }

        protected internal override void HandleHoverLeave()
        {
            Point at = lastHover;
            Raise("OnHover", () => implementation.OnHover(at.X, at.Y, false));
            base.HandleHoverLeave();
        }

        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (e.Target == this && Raise("OnKey", () => implementation.OnKey(e.Key, e.Shift, e.Ctrl), false))
            {
                e.Handled = true;
            }
            return base.HandleKey(e);
        }
    }
}
