using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Utilities;
using UIFramework.Api;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>
    /// A HUD widget: a hidden <see cref="UIMenu"/> that is never opened as an <c>IClickableMenu</c>. The inner menu
    /// supplies the tree, layout (anchor + offset, size policy), focus / overlay / router services and the consumer
    /// context; <see cref="HudService"/> lays it out, draws it from <c>Display.RenderedHud</c> and routes input to it.
    /// One widget serves every split-screen player: hover, focus and overlay are per screen (the inner menu's view),
    /// and so is whether the widget is shown; the tree is laid out again for each screen.
    /// </summary>
    internal sealed class UIHud : IUIHud
    {
        private const int BoxPadding = 16;

        private bool drawBox = true;
        private float opacity = 1f;
        private readonly PerScreen<bool> shown = new();

        /// <summary>The frame <see cref="OnUpdate"/> last ran in (it runs once per game tick, not once per screen).</summary>
        private long lastUpdateFrame = long.MinValue;

        internal UIHud(string id, ConsumerContext consumer, MenuRegistry registry)
        {
            Id = id;
            Inner = new UIMenu("hud:" + id, consumer, registry)
            {
                DrawBox = false,
                Modal = false,
                DimBackground = false,
                ShowCloseButton = false,
                CloseOnEscape = false,
                PlayerLayout = false,
                Padding = BoxPadding,
                Anchor = UIAnchor.TopLeft
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Identity
        // ---------------------------------------------------------------------------------------------------------

        public string Id { get; }

        /// <summary>The hidden menu model that owns the tree and the per-menu services.</summary>
        internal UIMenu Inner { get; }

        internal ConsumerContext Consumer => Inner.Consumer;

        IUIStack IUIHud.Root => Inner.Root;

        // ---------------------------------------------------------------------------------------------------------
        //  Options
        // ---------------------------------------------------------------------------------------------------------

        public bool Visible { get; set; } = true;

        public UIAnchor Anchor
        {
            get => Inner.Anchor;
            set => Inner.Anchor = value;
        }

        public int X
        {
            get => Inner.Anchor == UIAnchor.Explicit ? Inner.X : Inner.AnchorOffset.X;
            set => SetOffset(value, Y);
        }

        public int Y
        {
            get => Inner.Anchor == UIAnchor.Explicit ? Inner.Y : Inner.AnchorOffset.Y;
            set => SetOffset(X, value);
        }

        /// <summary>Set both offsets (explicit position for <see cref="UIAnchor.Explicit"/>) without changing the anchor.</summary>
        internal void SetOffset(int px, int py)
        {
            Inner.X = px;
            Inner.Y = py;
            Inner.AnchorOffset = new Point(px, py);
        }

        public int? Width
        {
            get => Inner.Width;
            set => Inner.Width = value;
        }

        public int? Height
        {
            get => Inner.Height;
            set => Inner.Height = value;
        }

        public bool DrawBox
        {
            get => drawBox;
            set
            {
                drawBox = value;
                Inner.Padding = value ? BoxPadding : 0;
            }
        }

        public float Opacity
        {
            get => opacity;
            set => opacity = Math.Clamp(value, 0f, 1f);
        }

        public bool Interactive { get; set; }

        /// <summary>Drawn on top of an open menu instead of hidden while one is open (input stays world-only).</summary>
        public bool ShowOverMenus { get; set; }

        internal Func<bool>? ShowWhen { get; set; }
        internal Action<IUIHud, double>? OnUpdate { get; set; }

        Func<bool> IUIHud.ShowWhen { get => ShowWhen!; set => ShowWhen = value; }
        Action<IUIHud, double> IUIHud.OnUpdate { get => OnUpdate!; set => OnUpdate = value; }

        /// <summary>The consumer's own offset, captured before a player layout was applied (null = none applied yet).</summary>
        internal WindowLayout? ConsumerLayout { get; set; }

        public Rectangle Bounds => Inner.Bounds;

        public IUIElement Find(string id) => Inner.Find(id ?? string.Empty);

        public void InvalidateLayout() => Inner.InvalidateLayout();

        // ---------------------------------------------------------------------------------------------------------
        //  Per-frame (called by HudService)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Whether the widget wants to be drawn on the current screen (consumer switch and <see cref="ShowWhen"/>), as of the last <see cref="EvaluateShown"/>.</summary>
        internal bool IsShown => shown.Value;

        /// <summary>Re-evaluate <see cref="Visible"/> and <see cref="ShowWhen"/> for the current screen (once per tick, through the callback guard).</summary>
        internal bool EvaluateShown()
        {
            bool value = Visible && (ShowWhen == null || Consumer.Invoke(Inner.Id, Inner.Id, "ShowWhen", ShowWhen, true));
            shown.Value = value;
            return value;
        }

        /// <summary>
        /// Layout if needed and update the tree (on every screen), then raise <see cref="OnUpdate"/> once per game tick:
        /// <paramref name="frame"/> identifies the tick and is the same for every split-screen player.
        /// </summary>
        internal void Tick(double elapsedMs, long frame)
        {
            Inner.Tick(elapsedMs);
            if (OnUpdate != null && frame != lastUpdateFrame)
            {
                lastUpdateFrame = frame;
                Action<IUIHud, double> cb = OnUpdate;
                Consumer.InvokeWith(Inner.Id, Inner.Id, "OnUpdate", static s => s.cb(s.hud, s.elapsedMs), (cb, hud: (IUIHud)this, elapsedMs));
            }
        }

        /// <summary>Draw the box, the tree, the overlay pass and (when interactive) the tooltip.</summary>
        internal void Draw(SpriteBatch b)
        {
            if (Inner.LayoutDirty)
            {
                Inner.Relayout();
            }

            if (drawBox)
            {
                DrawHelper.PanelBox(b, Inner.Bounds, Color.White * opacity);
            }

            Inner.Viewport.Draw(b);
            Inner.Overlay.Draw(b);
            if (Interactive)
            {
                Inner.DrawTooltip(b);
            }

            if (UIServices.Config.DebugOverlay)
            {
                DrawHelper.DebugBounds(b, Inner.Bounds, Inner.Id, Color.Orange);
            }
        }

        /// <summary>Drop hover state when the widget stops receiving input (hidden, menu opened, made non-interactive).</summary>
        internal void ReleaseInput()
        {
            Inner.Router.SetHovered(null);
            Inner.Router.ClickReleased(Inner.CursorX, Inner.CursorY);
            Inner.Focus.ClearFocus();
            Inner.Overlay.CloseAll();
        }

        public override string ToString() => $"UIHud('{Id}' of {Consumer.ModId})";
    }
}
