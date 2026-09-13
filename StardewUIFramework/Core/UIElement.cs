using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using UIFramework.Api;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>
    /// Base class of every node in a menu tree. Consumers only ever see it through <see cref="IUIElement"/>
    /// (or one of the derived interfaces).
    /// <para>
    /// Layout is two-phase: <see cref="Measure"/> reports the desired size (including margins) for an available size,
    /// then <see cref="Arrange"/> receives the absolute slot the parent allotted and resolves <see cref="Bounds"/>.
    /// Input arrives through the <c>Handle*</c> virtuals from <see cref="EventRouter"/>; each built-in behaviour runs
    /// first and then raises the matching consumer callback through the owning <see cref="ConsumerContext"/>.
    /// </para>
    /// </summary>
    internal abstract class UIElement : IUIElement
    {
        private bool visible = true;
        private bool enabled = true;
        private int marginLeft, marginTop, marginRight, marginBottom;
        private int? width, height;
        private UIAlign horizontalAlign = UIAlign.Start;
        private UIAlign verticalAlign = UIAlign.Start;
        private int x, y, row, column, rowSpan = 1, columnSpan = 1;

        protected UIElement(string id)
        {
            Id = id ?? string.Empty;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Tree
        // ---------------------------------------------------------------------------------------------------------

        public string Id { get; }

        /// <summary>Parent container, or null for a root.</summary>
        public UIContainer? ParentElement { get; internal set; }

        /// <summary>The menu this element is attached to (null while detached).</summary>
        public UIMenu? OwnerMenu { get; private set; }

        /// <summary>Consumer that owns the menu (never null; <see cref="ConsumerContext.None"/> while detached).</summary>
        public ConsumerContext Consumer => OwnerMenu?.Consumer ?? ConsumerContext.None;

        IUIContainer IUIElement.Parent => ParentElement!;
        IUIMenu IUIElement.Menu => OwnerMenu!;

        /// <summary>Called when the element is attached to / detached from a menu tree.</summary>
        internal virtual void SetOwnerMenu(UIMenu? menu)
        {
            if (OwnerMenu == menu)
                return;
            OwnerMenu?.OnElementDetached(this);
            OwnerMenu = menu;
        }

        /// <summary>This element and every descendant, depth first.</summary>
        public virtual IEnumerable<UIElement> SelfAndDescendants()
        {
            yield return this;
        }

        /// <summary>Find a descendant (or this element) by id.</summary>
        public UIElement? FindById(string id)
        {
            foreach (UIElement e in SelfAndDescendants())
            {
                if (e.Id == id)
                    return e;
            }
            return null;
        }

        /// <summary>True if <paramref name="other"/> is this element or one of its ancestors.</summary>
        public bool IsSelfOrDescendantOf(UIElement other)
        {
            for (UIElement? e = this; e != null; e = e.ParentElement)
            {
                if (e == other)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        public Rectangle Bounds { get; protected set; }

        public bool Visible
        {
            get => visible;
            set
            {
                if (visible == value)
                    return;
                visible = value;
                InvalidateLayout();
            }
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                    return;
                enabled = value;
                if (!enabled && IsFocused)
                    OwnerMenu?.Focus.ClearFocus();
            }
        }

        public Func<string>? Tooltip { get; set; }
        public Func<string>? TooltipTitle { get; set; }
        public object? Tag { get; set; }
        public UIStyle? StyleObject { get; set; }

        Func<string> IUIElement.Tooltip { get => Tooltip!; set => Tooltip = value; }
        Func<string> IUIElement.TooltipTitle { get => TooltipTitle!; set => TooltipTitle = value; }
        object IUIElement.Tag { get => Tag!; set => Tag = value; }

        IUIStyle IUIElement.Style
        {
            get => StyleObject!;
            set
            {
                StyleObject = value as UIStyle;
                InvalidateLayout();
            }
        }

        public int MarginLeft { get => marginLeft; set => SetLayoutField(ref marginLeft, value); }
        public int MarginTop { get => marginTop; set => SetLayoutField(ref marginTop, value); }
        public int MarginRight { get => marginRight; set => SetLayoutField(ref marginRight, value); }
        public int MarginBottom { get => marginBottom; set => SetLayoutField(ref marginBottom, value); }

        public void SetMargin(int all) => SetMargin(all, all, all, all);
        public void SetMargin(int horizontal, int vertical) => SetMargin(horizontal, vertical, horizontal, vertical);

        public void SetMargin(int left, int top, int right, int bottom)
        {
            marginLeft = left;
            marginTop = top;
            marginRight = right;
            marginBottom = bottom;
            InvalidateLayout();
        }

        public int? Width
        {
            get => width;
            set
            {
                width = value;
                InvalidateLayout();
            }
        }

        public int? Height
        {
            get => height;
            set
            {
                height = value;
                InvalidateLayout();
            }
        }

        /// <summary>True once the consumer explicitly set <see cref="HorizontalAlign"/>.</summary>
        internal bool HorizontalAlignSet { get; private set; }

        /// <summary>True once the consumer explicitly set <see cref="VerticalAlign"/>.</summary>
        internal bool VerticalAlignSet { get; private set; }

        public UIAlign HorizontalAlign
        {
            get => horizontalAlign;
            set
            {
                horizontalAlign = value;
                HorizontalAlignSet = true;
                InvalidateLayout();
            }
        }

        public UIAlign VerticalAlign
        {
            get => verticalAlign;
            set
            {
                verticalAlign = value;
                VerticalAlignSet = true;
                InvalidateLayout();
            }
        }

        /// <summary>Alignment actually used: the element's own if set, otherwise the parent's default for it.</summary>
        internal UIAlign ResolvedHorizontalAlign => HorizontalAlignSet ? horizontalAlign : ParentElement?.DefaultChildHorizontalAlign(this) ?? horizontalAlign;

        internal UIAlign ResolvedVerticalAlign => VerticalAlignSet ? verticalAlign : ParentElement?.DefaultChildVerticalAlign(this) ?? verticalAlign;

        public int X { get => x; set => SetLayoutField(ref x, value); }
        public int Y { get => y; set => SetLayoutField(ref y, value); }
        public int Row { get => row; set => SetLayoutField(ref row, Math.Max(0, value)); }
        public int Column { get => column; set => SetLayoutField(ref column, Math.Max(0, value)); }
        public int RowSpan { get => rowSpan; set => SetLayoutField(ref rowSpan, Math.Max(1, value)); }
        public int ColumnSpan { get => columnSpan; set => SetLayoutField(ref columnSpan, Math.Max(1, value)); }

        private void SetLayoutField(ref int field, int value)
        {
            if (field == value)
                return;
            field = value;
            InvalidateLayout();
        }

        public bool IsFocused => OwnerMenu?.Focus.Focused == this;
        public bool IsHovered => OwnerMenu?.Hovered == this;

        public void Focus()
        {
            if (Focusable && OwnerMenu != null)
                OwnerMenu.Focus.SetFocus(this);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Consumer callbacks
        // ---------------------------------------------------------------------------------------------------------

        public Action<IUIClickEvent>? OnClick { get; set; }
        public Action<IUIClickEvent>? OnRightClick { get; set; }
        public Action<IUIElement>? OnHover { get; set; }
        public Action<IUIElement>? OnHoverEnd { get; set; }
        public Action<IUIElement>? OnFocus { get; set; }
        public Action<IUIElement>? OnBlur { get; set; }
        public Func<IUIKeyEvent, bool>? OnKey { get; set; }
        public Action<SpriteBatch, Rectangle>? OnDrawExtra { get; set; }
        public Action<SpriteBatch, Rectangle>? OnDrawOverlay { get; set; }

        Action<IUIClickEvent> IUIElement.OnClick { get => OnClick!; set => OnClick = value; }
        Action<IUIClickEvent> IUIElement.OnRightClick { get => OnRightClick!; set => OnRightClick = value; }
        Action<IUIElement> IUIElement.OnHover { get => OnHover!; set => OnHover = value; }
        Action<IUIElement> IUIElement.OnHoverEnd { get => OnHoverEnd!; set => OnHoverEnd = value; }
        Action<IUIElement> IUIElement.OnFocus { get => OnFocus!; set => OnFocus = value; }
        Action<IUIElement> IUIElement.OnBlur { get => OnBlur!; set => OnBlur = value; }
        Func<IUIKeyEvent, bool> IUIElement.OnKey { get => OnKey!; set => OnKey = value; }
        Action<SpriteBatch, Rectangle> IUIElement.OnDrawExtra { get => OnDrawExtra!; set => OnDrawExtra = value; }
        Action<SpriteBatch, Rectangle> IUIElement.OnDrawOverlay { get => OnDrawOverlay!; set => OnDrawOverlay = value; }

        /// <summary>Invoke a consumer callback through the guard.</summary>
        protected void Raise(string eventName, Action? action) => Consumer.Invoke(Id, eventName, action);

        /// <summary>Invoke a consumer callback that returns a value through the guard.</summary>
        protected T Raise<T>(string eventName, Func<T>? func, T fallback) => Consumer.Invoke(Id, eventName, func, fallback);

        // ---------------------------------------------------------------------------------------------------------
        //  Style
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Theme → consumer default → own style, fully resolved.</summary>
        protected ResolvedStyle Style
        {
            get
            {
                UIStyle merged = Theme.Default.Merge(Consumer.DefaultStyle).Merge(StyleObject);
                return new ResolvedStyle(merged);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Desired size including margins, valid after <see cref="Measure"/>.</summary>
        public Vector2 DesiredSize { get; private set; }

        /// <summary>Set when a property affecting layout changed; cleared by <see cref="Arrange"/>.</summary>
        public bool LayoutDirty { get; private set; } = true;

        public void InvalidateLayout()
        {
            LayoutDirty = true;
            ParentElement?.InvalidateLayout();
            if (ParentElement == null)
                OwnerMenu?.MarkLayoutDirty();
        }

        /// <summary>Measure pass: returns the desired size (including margins) for <paramref name="available"/> (including margins).</summary>
        public Vector2 Measure(Vector2 available)
        {
            if (!Visible)
            {
                DesiredSize = Vector2.Zero;
                return DesiredSize;
            }

            var inner = new Vector2(
                Math.Max(0, available.X - marginLeft - marginRight),
                Math.Max(0, available.Y - marginTop - marginBottom));
            if (width.HasValue)
                inner.X = width.Value;
            if (height.HasValue)
                inner.Y = height.Value;

            Vector2 core = MeasureCore(inner);
            if (width.HasValue)
                core.X = width.Value;
            if (height.HasValue)
                core.Y = height.Value;

            DesiredSize = new Vector2(
                Math.Max(0, core.X) + marginLeft + marginRight,
                Math.Max(0, core.Y) + marginTop + marginBottom);
            return DesiredSize;
        }

        /// <summary>Report the content size (without margins) for the given content-available size.</summary>
        protected abstract Vector2 MeasureCore(Vector2 available);

        /// <summary>Arrange pass: <paramref name="slot"/> is the absolute rectangle allotted by the parent (including margins).</summary>
        public void Arrange(Rectangle slot)
        {
            if (!Visible)
            {
                Bounds = new Rectangle(slot.X, slot.Y, 0, 0);
                LayoutDirty = false;
                return;
            }

            int availW = Math.Max(0, slot.Width - marginLeft - marginRight);
            int availH = Math.Max(0, slot.Height - marginTop - marginBottom);
            int desiredW = Math.Max(0, (int)Math.Ceiling(DesiredSize.X) - marginLeft - marginRight);
            int desiredH = Math.Max(0, (int)Math.Ceiling(DesiredSize.Y) - marginTop - marginBottom);

            UIAlign ha = ResolvedHorizontalAlign;
            UIAlign va = ResolvedVerticalAlign;

            int w = width ?? (ha == UIAlign.Stretch ? availW : Math.Min(desiredW, availW));
            int h = height ?? (va == UIAlign.Stretch ? availH : Math.Min(desiredH, availH));

            int px = slot.X + marginLeft + LayoutEngine.AlignOffset(ha, availW, w);
            int py = slot.Y + marginTop + LayoutEngine.AlignOffset(va, availH, h);

            Bounds = new Rectangle(px, py, w, h);
            ArrangeCore();
            LayoutDirty = false;
        }

        /// <summary>Position children (containers) now that <see cref="Bounds"/> is final.</summary>
        protected virtual void ArrangeCore()
        {
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Draw / update
        // ---------------------------------------------------------------------------------------------------------

        public void Draw(SpriteBatch b)
        {
            if (!Visible)
                return;
            DrawCore(b);
            if (OnDrawExtra != null)
            {
                Action<SpriteBatch, Rectangle> cb = OnDrawExtra;
                Rectangle bounds = Bounds;
                Raise("OnDrawExtra", () => cb(b, bounds));
            }
            if (OnDrawOverlay != null && OwnerMenu != null)
            {
                Action<SpriteBatch, Rectangle> cb = OnDrawOverlay;
                Rectangle bounds = Bounds;
                OwnerMenu.Overlay.RegisterDraw(sb => Raise("OnDrawOverlay", () => cb(sb, bounds)));
            }
            if (UIServices.Config.DebugOverlay)
                DrawHelper.DebugBounds(b, Bounds, Id);
        }

        protected abstract void DrawCore(SpriteBatch b);

        /// <summary>Per-tick update (caret blink, animations). Containers forward to children.</summary>
        public virtual void Update(double elapsedMs)
        {
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Hit testing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether this element can be the target of pointer events at all (spacers are not).</summary>
        protected virtual bool IsHitTestVisible => true;

        /// <summary>Return the deepest visible + enabled element under the point, or null.</summary>
        public virtual UIElement? HitTest(int px, int py)
        {
            if (!Visible || !Enabled || !IsHitTestVisible)
                return null;
            return Bounds.Contains(px, py) ? this : null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input (called by EventRouter; built-in behaviour first, then consumer callbacks)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether clicking gives keyboard focus.</summary>
        public virtual bool Focusable => false;

        /// <summary>Whether the element wants <see cref="StardewValley.IKeyboardSubscriber"/> text input while focused.</summary>
        public virtual bool WantsTextInput => false;

        /// <summary>Whether this element acts as the target for keyboard "activate" (Enter / gamepad A) — buttons do.</summary>
        public virtual bool ActivateOnEnter => false;

        /// <summary>Left or right click on (or bubbling through) this element. Return true to stop bubbling.</summary>
        protected internal virtual bool HandleClick(UIClickEvent e)
        {
            RaiseClickCallbacks(e);
            return e.Handled;
        }

        /// <summary>Raise <see cref="OnClick"/> / <see cref="OnRightClick"/> for <paramref name="e"/> if this element is its target.</summary>
        protected void RaiseClickCallbacks(UIClickEvent e)
        {
            Action<IUIClickEvent>? cb = e.Button == UIMouseButton.Left ? OnClick : OnRightClick;
            if (cb != null)
                Raise(e.Button == UIMouseButton.Left ? "OnClick" : "OnRightClick", () => cb(e));
        }

        /// <summary>Mouse button held after a click that targeted this element (drag).</summary>
        protected internal virtual void HandleClickHeld(int px, int py)
        {
        }

        /// <summary>Mouse button released after a click that targeted this element.</summary>
        protected internal virtual void HandleClickRelease(int px, int py)
        {
        }

        protected internal virtual void HandleHoverEnter()
        {
            string? cue = HoverSoundCue;
            if (!string.IsNullOrEmpty(cue))
                UIServices.PlaySound(cue);
            if (OnHover != null)
                Raise("OnHover", () => OnHover(this));
        }

        protected internal virtual void HandleHoverMove(int px, int py)
        {
        }

        protected internal virtual void HandleHoverLeave()
        {
            if (OnHoverEnd != null)
                Raise("OnHoverEnd", () => OnHoverEnd(this));
        }

        /// <summary>Sound played when the cursor enters (null / empty = none).</summary>
        protected virtual string? HoverSoundCue => null;

        /// <summary>Scroll wheel while the cursor is over this element (bubbles). <paramref name="direction"/> &gt; 0 = up.</summary>
        protected internal virtual bool HandleScroll(int direction)
        {
            return false;
        }

        /// <summary>Key press while focused (or bubbling from a focused descendant). Return true to stop.</summary>
        protected internal virtual bool HandleKey(UIKeyEvent e)
        {
            if (OnKey != null)
            {
                Func<IUIKeyEvent, bool> cb = OnKey;
                if (Raise("OnKey", () => cb(e), false))
                    e.Handled = true;
            }
            return e.Handled;
        }

        /// <summary>Enter / gamepad A while focused. Return true if consumed.</summary>
        protected internal virtual bool HandleActivate()
        {
            return false;
        }

        // keyboard subscriber forwarding (only while focused and WantsTextInput)
        protected internal virtual void HandleTextInput(char c)
        {
        }

        protected internal virtual void HandleTextInput(string text)
        {
            foreach (char c in text)
                HandleTextInput(c);
        }

        protected internal virtual void HandleCommandInput(char command)
        {
        }

        protected internal virtual void HandleSpecialInput(Keys key)
        {
        }

        protected internal virtual void HandleFocusGained()
        {
            if (OnFocus != null)
                Raise("OnFocus", () => OnFocus(this));
        }

        protected internal virtual void HandleFocusLost()
        {
            if (OnBlur != null)
                Raise("OnBlur", () => OnBlur(this));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Overlay popups (dropdown lists etc.). Only elements that call OverlayLayer.OpenPopup use these.
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Absolute bounds of the element's open popup.</summary>
        protected internal virtual Rectangle PopupBounds => Rectangle.Empty;

        protected internal virtual void DrawPopup(SpriteBatch b)
        {
        }

        protected internal virtual void HandlePopupClick(UIClickEvent e)
        {
        }

        protected internal virtual void HandlePopupHover(int px, int py)
        {
        }

        protected internal virtual bool HandlePopupScroll(int direction)
        {
            return false;
        }

        /// <summary>Close the popup (clicked outside, another popup opened, menu closing).</summary>
        protected internal virtual void ClosePopup()
        {
        }

        public override string ToString() => $"{GetType().Name}('{Id}')";
    }
}
