using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// A custom component that embeds built-in elements (architecture.md §16.1, "Custom components that embed
    /// built-ins"). The consumer's <see cref="IUICustomComponent"/> draws its chrome and handles its own clicks;
    /// the framework owns a child <see cref="Host"/> container (<c>"&lt;id&gt;.host"</c>, a column) that the consumer
    /// fills once through a build callback, and lays out, draws, focuses and hit-tests those children like any other.
    /// The element measures as the larger of the implementation's size and the host's (the host alone when the
    /// implementation reports no size), the host is arranged over the whole bounds, and the implementation draws first.
    /// </summary>
    internal sealed class CustomHostAdapter : UIContainer
    {
        private readonly CustomComponentBridge bridge;

        internal CustomHostAdapter(string id, IUICustomComponent implementation) : base(id)
        {
            bridge = new CustomComponentBridge(this, implementation);
            Host = new Stack(id + ".host", horizontal: false, spacing: 0)
            {
                HorizontalAlign = UIAlign.Stretch,
                VerticalAlign = UIAlign.Stretch
            };
            Add(Host);
        }

        /// <summary>The container the consumer fills with built-in elements.</summary>
        internal Stack Host { get; }

        /// <summary>Run the consumer's build callback once, guarded.</summary>
        internal void Build(Action<IUIContainer> build)
        {
            Raise("Custom.Build", () => build(Host));
        }

        internal override bool Focusable => bridge.WantsFocus;

        /// <summary>Like <see cref="CustomElementAdapter"/>: the consumer's <c>AccessibleName</c> or the tooltip describes it.</summary>
        internal override string AccessibleDescription
        {
            get
            {
                string? tooltip = Tooltip == null ? null : Raise("Tooltip", Tooltip, string.Empty);
                return Accessibility.Compose(Accessibility.Text("custom", "Custom"), tooltip ?? Id);
            }
        }

        /// <summary>Overlay components draw (chrome, children, <c>OnDrawExtra</c>) and are hit-tested above the tree.</summary>
        protected override bool DrawsInOverlay => bridge.WantsOverlay;

        protected override Vector2 MeasureCore(Vector2 available)
        {
            Vector2 size = bridge.Measure(available);
            foreach (UIElement child in Children)
            {
                Vector2 childSize = child.Measure(available);
                size.X = Math.Max(size.X, childSize.X);
                size.Y = Math.Max(size.Y, childSize.Y);
            }
            return size;
        }

        /// <summary>Like the measure: the larger of the implementation's reported minimum and the host's.</summary>
        protected override float MinWidthCore() => Math.Max(bridge.MinimumWidth, MaxChildMinWidth());

        protected override void ArrangeCore()
        {
            foreach (UIElement child in Children)
            {
                child.Arrange(Bounds);
            }
        }

        protected override void DrawCore(SpriteBatch b)
        {
            bridge.Draw(b, Bounds);
            DrawChildren(b);
        }

        internal override void Update(double elapsedMs)
        {
            bridge.Update(Bounds, elapsedMs);
            base.Update(elapsedMs);
        }

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
