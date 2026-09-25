using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>Base class for elements with children. Derived classes implement the layout algorithm.</summary>
    internal abstract class UIContainer : UIElement, IUIContainer
    {
        private readonly List<UIElement> children = new();

        protected UIContainer(string id) : base(id) { }

        /// <summary>Children in add (= draw / layout) order. Do not mutate; use Add/Remove.</summary>
        internal IReadOnlyList<UIElement> Children => children;

        public int ChildCount => children.Count;

        public IUIElement GetChild(int index) => children[index];

        void IUIContainer.Add(IUIElement child) => Add(Unwrap(child));
        void IUIContainer.Remove(IUIElement child) => Remove(Unwrap(child));

        /// <summary>Consumers hand back proxies of our own objects; Pintail unwraps them, but be defensive.</summary>
        internal static UIElement Unwrap(IUIElement element)
        {
            return element as UIElement ?? throw new ArgumentException("The element was not created by this framework.", nameof(element));
        }

        internal void Add(UIElement child)
        {
            ArgumentNullException.ThrowIfNull(child);

            if (child == this || IsSelfOrDescendantOf(child))
            {
                throw new InvalidOperationException($"Cannot add '{child.Id}' to '{Id}': it would create a cycle.");
            }

            child.ParentElement?.Remove(child);
            children.Add(child);
            child.ParentElement = this;
            child.SetOwnerMenu(OwnerMenu);
            InvalidateLayout();
        }

        internal void Insert(int index, UIElement child)
        {
            Add(child);
            children.Remove(child);
            children.Insert(Math.Clamp(index, 0, children.Count), child);
        }

        internal void Remove(UIElement child)
        {
            if (!children.Remove(child))
            {
                return;
            }

            child.ParentElement = null;
            child.SetOwnerMenu(null);
            InvalidateLayout();
        }

        public void Clear()
        {
            foreach (UIElement child in children.ToArray())
            {
                children.Remove(child);
                child.ParentElement = null;
                child.SetOwnerMenu(null);
            }
            InvalidateLayout();
        }

        internal override void SetOwnerMenu(UIMenu? menu)
        {
            base.SetOwnerMenu(menu);
            foreach (UIElement child in children)
            {
                child.SetOwnerMenu(menu);
            }
        }

        internal override IEnumerable<UIElement> SelfAndDescendants()
        {
            yield return this;
            foreach (UIElement child in children)
            {
                foreach (UIElement e in child.SelfAndDescendants())
                {
                    yield return e;
                }
            }
        }

        // COMPOSITES
        /// <summary>
        /// The mod that fills this container through its own API instance (a composite's defining mod), or null.
        /// That mod may add children below this container even though the menu belongs to another mod.
        /// </summary>
        internal virtual ConsumerContext? ComponentOwner => null;

        /// <summary>
        /// The widest minimum width among the children (hidden ones report 0): the minimum of every container whose
        /// children share its full width (overlapping panels, columns).
        /// </summary>
        protected float MaxChildMinWidth()
        {
            float min = 0;
            foreach (UIElement child in children)
            {
                min = Math.Max(min, child.MeasureMinWidth());
            }

            return min;
        }

        /// <summary>Default horizontal alignment for a child that did not set its own (stacks use this for the cross axis).</summary>
        internal virtual UIAlign DefaultChildHorizontalAlign(UIElement child) => UIAlign.Start;

        internal virtual UIAlign DefaultChildVerticalAlign(UIElement child) => UIAlign.Start;

        /// <summary>Containers themselves are only hit when no child is, and only if they have a click / tooltip reason to be.</summary>
        internal override UIElement? HitTest(int px, int py)
        {
            if (!Visible || !Enabled || !Bounds.Contains(px, py))
            {
                return null;
            }

            for (int i = children.Count - 1; i >= 0; i--)
            {
                UIElement? hit = children[i].HitTest(px, py);
                if (hit != null)
                {
                    return hit;
                }
            }
            return IsHitTestVisible ? this : null;
        }

        protected override void DrawCore(SpriteBatch b)
        {
            DrawChildren(b);
        }

        protected void DrawChildren(SpriteBatch b)
        {
            // index loop: a consumer callback raised from a child's draw may mutate the tree
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Draw(b);
            }
        }

        internal override void Update(double elapsedMs)
        {
            for (int i = 0; i < children.Count; i++)
            {
                children[i].Update(elapsedMs);
            }
        }
    }
}
