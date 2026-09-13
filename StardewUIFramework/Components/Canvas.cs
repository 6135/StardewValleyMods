using System;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>Places each child at its own <see cref="UIElement.X"/> / <see cref="UIElement.Y"/> offset (pixel-exact escape hatch).</summary>
    internal sealed class Canvas : UIContainer, IUICanvas
    {
        public Canvas(string id) : base(id) { }

        protected override bool IsHitTestVisible => OnClick != null || OnRightClick != null || Tooltip != null || OnHover != null;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            float w = 0, h = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                    continue;
                Vector2 size = child.Measure(new Vector2(Math.Max(0, available.X - child.X), Math.Max(0, available.Y - child.Y)));
                w = Math.Max(w, child.X + size.X);
                h = Math.Max(h, child.Y + size.Y);
            }
            return new Vector2(w, h);
        }

        protected override void ArrangeCore()
        {
            foreach (UIElement child in Children)
            {
                int w = (int)Math.Ceiling(child.DesiredSize.X);
                int h = (int)Math.Ceiling(child.DesiredSize.Y);
                // stretched children fill the remaining canvas area from their offset
                if (child.ResolvedHorizontalAlign == UIAlign.Stretch)
                    w = Math.Max(w, Bounds.Width - child.X);
                if (child.ResolvedVerticalAlign == UIAlign.Stretch)
                    h = Math.Max(h, Bounds.Height - child.Y);
                child.Arrange(new Rectangle(Bounds.X + child.X, Bounds.Y + child.Y, w, h));
            }
        }
    }
}
