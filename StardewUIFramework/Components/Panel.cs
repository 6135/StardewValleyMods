using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>A padded box with an optional vanilla 9-slice background. Children overlap and each fills the padded area.</summary>
    internal sealed class Panel : UIContainer, IUIPanel
    {
        private bool drawBox;
        private int padding;

        internal Panel(string id, bool drawBox, int padding) : base(id)
        {
            this.drawBox = drawBox;
            this.padding = Math.Max(0, padding);
        }

        public bool DrawBox
        {
            get => drawBox;
            set => drawBox = value;
        }

        public int Padding
        {
            get => padding;
            set
            {
                value = Math.Max(0, value);
                if (padding == value)
                {
                    return;
                }

                padding = value;
                InvalidateLayout();
            }
        }

        /// <summary>Effective padding: the explicit value, or the style's padding when none was given, scaled by the theme.</summary>
        private int EffectivePadding => Theme.Space(padding > 0 ? padding : Style.Padding ?? 0);

        // a bare panel (no box, no handlers) lets clicks fall through so empty space clears focus
        protected override bool IsHitTestVisible => drawBox || HasPointerHandlers;

        /// <summary>Absolute content rectangle (bounds minus padding).</summary>
        internal Rectangle ContentBounds
        {
            get
            {
                int p = EffectivePadding;
                return new Rectangle(Bounds.X + p, Bounds.Y + p, Math.Max(0, Bounds.Width - (2 * p)), Math.Max(0, Bounds.Height - (2 * p)));
            }
        }

        protected override Vector2 MeasureCore(Vector2 available)
        {
            int p = EffectivePadding;
            var inner = new Vector2(Math.Max(0, available.X - (2 * p)), Math.Max(0, available.Y - (2 * p)));
            float w = 0, h = 0;
            foreach (UIElement child in Children)
            {
                Vector2 size = child.Measure(inner);
                w = Math.Max(w, size.X);
                h = Math.Max(h, size.Y);
            }
            return new Vector2(w + (2 * p), h + (2 * p));
        }

        protected override void ArrangeCore()
        {
            Rectangle content = ContentBounds;
            foreach (UIElement child in Children)
            {
                child.Arrange(content);
            }
        }

        protected override void DrawCore(SpriteBatch b)
        {
            if (drawBox)
            {
                DrawHelper.StyledBox(b, Style, button: false, Bounds, Color.White);
            }
            DrawChildren(b);
        }
    }
}
