using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// The guarded calls into a consumer-implemented <see cref="IUICustomComponent"/>, shared by
    /// <see cref="CustomElementAdapter"/> and <see cref="CustomHostAdapter"/>. Every call crosses the API proxy and
    /// runs through the owning element's <see cref="ConsumerContext"/>, so a faulting component is logged and muted
    /// instead of crashing the game loop. The calls use their own <c>Custom.*</c> event names so muting one never
    /// silences the element's public <see cref="IUIElement.OnClick"/> / <see cref="IUIElement.OnHover"/> /
    /// <see cref="IUIElement.OnKey"/> callbacks (or the reverse).
    /// </summary>
    internal sealed class CustomComponentBridge
    {
        private readonly UIElement owner;
        private readonly IUICustomComponent implementation;
        private Point lastHover;

        internal CustomComponentBridge(UIElement owner, IUICustomComponent implementation)
        {
            this.owner = owner;
            this.implementation = implementation;
        }

        internal bool WantsFocus => Get("Custom.WantsFocus", () => implementation.WantsFocus, false);

        internal bool WantsOverlay => Get("Custom.WantsOverlay", () => implementation.WantsOverlay, false);

        /// <summary>The implementation's desired size, never negative.</summary>
        internal Vector2 Measure(Vector2 available)
        {
            Vector2 size = Get("Custom.Measure", () => implementation.Measure(available), Vector2.Zero);
            return new Vector2(System.Math.Max(0, size.X), System.Math.Max(0, size.Y));
        }

        /// <summary>The narrowest width the implementation can be drawn at (<see cref="IUICustomComponent.MinimumWidth"/>), never negative.</summary>
        internal float MinimumWidth => System.Math.Max(0, Get("Custom.MinimumWidth", () => implementation.MinimumWidth, 0f));

        internal void Draw(SpriteBatch b, Rectangle bounds) => Run("Custom.Draw", () => implementation.Draw(b, bounds));

        internal void Update(Rectangle bounds, double elapsedMs) => Run("Custom.Update", () => implementation.Update(bounds, elapsedMs));

        /// <summary>Forward a click that targets the owner; returns true when the implementation handled it.</summary>
        internal bool Click(UIClickEvent e)
        {
            return e.Target == owner && Get("Custom.OnClick", () => implementation.OnClick(e.X, e.Y, e.Button == UIMouseButton.Right), false);
        }

        // the router raises Enter and then Move(x, y) in the same pass, so Enter needs no call of its own
        internal void HoverMove(int px, int py)
        {
            lastHover = new Point(px, py);
            Run("Custom.OnHover", () => implementation.OnHover(px, py, true));
        }

        internal void HoverLeave()
        {
            Point at = lastHover;
            Run("Custom.OnHoverEnd", () => implementation.OnHover(at.X, at.Y, false));
        }

        /// <summary>Forward a key press that targets the owner; returns true when the implementation handled it.</summary>
        internal bool Key(UIKeyEvent e)
        {
            return e.Target == owner && Get("Custom.OnKey", () => implementation.OnKey(e.Key, e.Shift, e.Ctrl), false);
        }

        private void Run(string eventName, System.Action action) => owner.Consumer.Invoke(owner.Id, eventName, action);

        private T Get<T>(string eventName, System.Func<T> func, T fallback) => owner.Consumer.Invoke(owner.Id, eventName, func, fallback);
    }
}
