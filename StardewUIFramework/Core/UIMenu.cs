using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>
    /// The model of a screen: a root container plus window chrome / position / size policy, lifecycle callbacks,
    /// and the per-menu services (focus, overlay, router). Re-opening reuses the model but creates a fresh
    /// <see cref="MenuHost"/>.
    /// </summary>
    internal sealed class UIMenu : IUIMenu
    {
        // chrome insets when DrawBox is on (vanilla dialogue box border + breathing room)
        private const int BoxInsetSide = 56; // IClickableMenu.spaceToClearSideBorder + borderWidth
        private const int BoxInsetTop = 80;
        private const int BoxInsetBottom = 56;
        private const int TitleReserve = 72;

        private readonly MenuRegistry registry;
        private int? width, height;
        private UIAnchor anchor = UIAnchor.Center;
        private int x, y, padding;
        private bool drawBox = true;
        private bool showCloseButton = true;
        private Func<string>? title;

        internal UIMenu(string id, ConsumerContext consumer, MenuRegistry registry)
        {
            Id = id;
            Consumer = consumer;
            this.registry = registry;
            Focus = new FocusManager(this);
            Overlay = new OverlayLayer();
            Router = new EventRouter(this);
            Root = new Stack(id + ".root", horizontal: false, spacing: 8)
            {
                HorizontalAlign = UIAlign.Stretch,
                VerticalAlign = UIAlign.Stretch
            };
            Root.SetOwnerMenu(this);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Identity / services
        // ---------------------------------------------------------------------------------------------------------

        public string Id { get; }
        internal ConsumerContext Consumer { get; }
        internal Stack Root { get; }
        internal FocusManager Focus { get; }
        internal OverlayLayer Overlay { get; }
        internal EventRouter Router { get; }

        /// <summary>The game-facing menu while open.</summary>
        internal MenuHost? Host { get; private set; }

        IUIStack IUIMenu.Root => Root;

        public bool IsOpen => Host != null;

        /// <summary>Element under the cursor (maintained by the router).</summary>
        internal UIElement? Hovered { get; set; }

        /// <summary>When the cursor entered <see cref="Hovered"/>.</summary>
        internal double HoverStartMs { get; set; }

        internal int CursorX { get; set; }
        internal int CursorY { get; set; }

        internal bool LayoutDirty { get; private set; } = true;

        // ---------------------------------------------------------------------------------------------------------
        //  Options
        // ---------------------------------------------------------------------------------------------------------

        internal Func<string>? TitleFunc
        {
            get => title;
            set
            {
                title = value;
                MarkLayoutDirty();
            }
        }

        Func<string> IUIMenu.Title { get => title!; set => TitleFunc = value; }

        public int? Width
        {
            get => width;
            set
            {
                width = value;
                MarkLayoutDirty();
            }
        }

        public int? Height
        {
            get => height;
            set
            {
                height = value;
                MarkLayoutDirty();
            }
        }

        public bool ShowCloseButton
        {
            get => showCloseButton;
            set
            {
                showCloseButton = value;
                Host?.SyncCloseButton();
            }
        }

        public bool Modal { get; set; } = true;
        public bool DimBackground { get; set; } = true;

        public UIAnchor Anchor
        {
            get => anchor;
            set
            {
                anchor = value;
                MarkLayoutDirty();
            }
        }

        public int X
        {
            get => x;
            set
            {
                x = value;
                MarkLayoutDirty();
            }
        }

        public int Y
        {
            get => y;
            set
            {
                y = value;
                MarkLayoutDirty();
            }
        }

        public bool DrawBox
        {
            get => drawBox;
            set
            {
                drawBox = value;
                MarkLayoutDirty();
            }
        }

        public int Padding
        {
            get => padding;
            set
            {
                padding = Math.Max(0, value);
                MarkLayoutDirty();
            }
        }

        public bool CloseOnEscape { get; set; } = true;

        public Rectangle Bounds { get; private set; }

        internal Button? DefaultButtonElement { get; set; }
        internal Button? CancelButtonElement { get; set; }

        IUIButton IUIMenu.DefaultButton { get => DefaultButtonElement!; set => DefaultButtonElement = value as Button; }
        IUIButton IUIMenu.CancelButton { get => CancelButtonElement!; set => CancelButtonElement = value as Button; }

        internal Action<IUIMenu>? OnOpen { get; set; }
        internal Action<IUIMenu>? OnClose { get; set; }
        internal Action<IUIMenu, double>? OnUpdate { get; set; }
        internal Func<IUIKeyEvent, bool>? OnKey { get; set; }
        internal Action<int>? OnScroll { get; set; }

        Action<IUIMenu> IUIMenu.OnOpen { get => OnOpen!; set => OnOpen = value; }
        Action<IUIMenu> IUIMenu.OnClose { get => OnClose!; set => OnClose = value; }
        Action<IUIMenu, double> IUIMenu.OnUpdate { get => OnUpdate!; set => OnUpdate = value; }
        Func<IUIKeyEvent, bool> IUIMenu.OnKey { get => OnKey!; set => OnKey = value; }
        Action<int> IUIMenu.OnScroll { get => OnScroll!; set => OnScroll = value; }

        public void SetPosition(int px, int py)
        {
            x = px;
            y = py;
            anchor = UIAnchor.Explicit;
            MarkLayoutDirty();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Tree
        // ---------------------------------------------------------------------------------------------------------

        public IUIElement Find(string id) => Root.FindById(id)!;

        internal void OnElementDetached(UIElement element)
        {
            if (Focus.Focused == element)
            {
                Focus.ClearFocus();
            }

            if (Hovered == element)
            {
                Hovered = null;
            }

            if (Router.Captured == element)
            {
                Router.ClickReleased(CursorX, CursorY);
            }

            Overlay.RemovePopup(element);
            if (DefaultButtonElement == element)
            {
                DefaultButtonElement = null;
            }

            if (CancelButtonElement == element)
            {
                CancelButtonElement = null;
            }
        }

        public void InvalidateLayout() => MarkLayoutDirty();

        internal void MarkLayoutDirty() => LayoutDirty = true;

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        private int InsetLeft => (drawBox ? BoxInsetSide : 0) + padding;
        private int InsetRight => (drawBox ? BoxInsetSide : 0) + padding;
        private int InsetTop => (drawBox ? BoxInsetTop : 0) + padding;
        private int InsetBottom => (drawBox ? BoxInsetBottom : 0) + padding;

        /// <summary>Measure the root, compute the menu rectangle from the size / position policy, arrange the tree.</summary>
        internal void Relayout()
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Layout);
            Point vp = UIServices.ViewportSize();
            int insetW = InsetLeft + InsetRight;
            int insetH = InsetTop + InsetBottom;

            float availW = (width ?? vp.X) - insetW;
            float availH = (height ?? vp.Y) - insetH;
            Root.Measure(new Vector2(Math.Max(0, availW), Math.Max(0, availH)));

            int w = width ?? (int)Math.Ceiling(Root.DesiredSize.X) + insetW;
            int h = height ?? (int)Math.Ceiling(Root.DesiredSize.Y) + insetH;
            w = Math.Clamp(w, Math.Min(insetW, vp.X), Math.Max(vp.X, 1));
            h = Math.Clamp(h, Math.Min(insetH, vp.Y), Math.Max(vp.Y, 1));

            Point position = ResolvePosition(vp, w, h);
            Bounds = new Rectangle(position.X, position.Y, w, h);
            Root.Arrange(new Rectangle(position.X + InsetLeft, position.Y + InsetTop, Math.Max(0, w - insetW), Math.Max(0, h - insetH)));
            LayoutDirty = false;

            Host?.SyncBounds();
            Focus.Validate();
        }

        /// <summary>Top-left corner for a menu of the given size: the anchor (or explicit X/Y), clamped to the viewport and below the title banner.</summary>
        private Point ResolvePosition(Point vp, int w, int h)
        {
            int minY = title != null && drawBox ? Math.Min(TitleReserve, Math.Max(0, vp.Y - h)) : 0;
            int px = anchor == UIAnchor.Explicit ? x : AnchorX(vp.X, w);
            int py = anchor == UIAnchor.Explicit ? y : AnchorY(vp.Y, h, minY);
            return new Point(
                Math.Clamp(px, 0, Math.Max(0, vp.X - w)),
                Math.Clamp(py, minY, Math.Max(minY, vp.Y - h)));
        }

        /// <summary>Left edge for the anchor: left column → 0, right column → flush right, otherwise centered.</summary>
        private int AnchorX(int viewportWidth, int w)
        {
            return anchor switch
            {
                UIAnchor.TopLeft or UIAnchor.MiddleLeft or UIAnchor.BottomLeft => 0,
                UIAnchor.TopRight or UIAnchor.MiddleRight or UIAnchor.BottomRight => viewportWidth - w,
                _ => (viewportWidth - w) / 2
            };
        }

        /// <summary>Top edge for the anchor: top row → below the title reserve, bottom row → flush bottom, otherwise centered.</summary>
        private int AnchorY(int viewportHeight, int h, int minY)
        {
            return anchor switch
            {
                UIAnchor.TopLeft or UIAnchor.TopCenter or UIAnchor.TopRight => minY,
                UIAnchor.BottomLeft or UIAnchor.BottomCenter or UIAnchor.BottomRight => viewportHeight - h,
                _ => (viewportHeight - h) / 2
            };
        }

        /// <summary>Absolute content rectangle (inside chrome and padding).</summary>
        internal Rectangle ContentBounds => new(Bounds.X + InsetLeft, Bounds.Y + InsetTop, Math.Max(0, Bounds.Width - InsetLeft - InsetRight), Math.Max(0, Bounds.Height - InsetTop - InsetBottom));

        // ---------------------------------------------------------------------------------------------------------
        //  Per-frame
        // ---------------------------------------------------------------------------------------------------------

        internal void Tick(double elapsedMs)
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Update);
            if (LayoutDirty)
            {
                Relayout();
            }

            Focus.Validate();
            Root.Update(elapsedMs);
            if (OnUpdate != null)
            {
                Action<IUIMenu, double> cb = OnUpdate;
                Consumer.Invoke(Id, "OnUpdate", () => cb(this, elapsedMs));
            }
            if (LayoutDirty)
            {
                Relayout();
            }
        }

        internal void Draw(SpriteBatch b)
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Draw, endsFrame: true);
            if (LayoutDirty)
            {
                Relayout();
            }

            Point vp = UIServices.ViewportSize();
            if (DimBackground && !Game1.options.showMenuBackground)
            {
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, vp.X, vp.Y), Color.Black * 0.4f);
            }

            if (drawBox)
            {
                Game1.drawDialogueBox(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, speaker: false, drawOnlyBox: true);
            }

            string? titleText = title == null ? null : Consumer.Invoke(Id, "Title", title, string.Empty);
            if (!string.IsNullOrEmpty(titleText))
            {
                if (drawBox)
                {
                    SpriteText.drawStringWithScrollCenteredAt(b, titleText, Bounds.Center.X, Math.Max(8, Bounds.Y - 56));
                }
                else
                {
                    DrawHelper.Text(b, titleText, UIFont.Dialogue, new Vector2(Bounds.Center.X - (UIServices.Text.Measure(UIFont.Dialogue, titleText, 1f).X / 2f), Bounds.Y + 8), Theme.TextColor, false, 1f);
                }
            }

            Root.Draw(b);
            InspectorRenderer.Draw(this, b);
            Overlay.Draw(b);
            DrawTooltip(b);

            if (UIServices.Config.DebugOverlay)
            {
                DrawHelper.DebugBounds(b, Bounds, Id, Color.Red);
            }
        }

        private void DrawTooltip(SpriteBatch b)
        {
            UIElement? hovered = Hovered;
            if (hovered == null || hovered.Tooltip == null || Overlay.HasPopups)
            {
                return;
            }

            if (UIServices.NowMs() - HoverStartMs < Consumer.EffectiveTooltipDelay)
            {
                return;
            }

            string text = Consumer.Invoke(hovered.Id, "Tooltip", hovered.Tooltip, string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string? tooltipTitle = hovered.TooltipTitle == null ? null : Consumer.Invoke(hovered.Id, "TooltipTitle", hovered.TooltipTitle, string.Empty);
            IClickableMenu.drawHoverText(b, text, Game1.smallFont, boldTitleText: string.IsNullOrEmpty(tooltipTitle) ? null : tooltipTitle);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Lifecycle
        // ---------------------------------------------------------------------------------------------------------

        public void Open(bool force)
        {
            if (IsOpen)
            {
                return;
            }

            if (!force && !Context.IsPlayerFree)
            {
                UIServices.Log($"[{Consumer.ModId}] menu '{Id}' was not opened because the player is not free (pass force = true to override).");
                return;
            }
            Host = new MenuHost(this);
            Game1.activeClickableMenu = Host;
            AfterOpened();
        }

        void IUIMenu.OpenAsChild(IUIMenu parent) => OpenAsChild(parent as UIMenu ?? throw new ArgumentException("The parent menu was not created by this framework.", nameof(parent)));

        internal void OpenAsChild(UIMenu parent)
        {
            if (IsOpen)
            {
                return;
            }

            if (!parent.IsOpen || parent.Host == null)
            {
                UIServices.Log($"[{Consumer.ModId}] menu '{Id}' cannot open as a child of '{parent.Id}' because the parent is not open.", LogLevel.Warn);
                return;
            }
            Host = new MenuHost(this);
            parent.Host.SetChildMenu(Host);
            AfterOpened();
        }

        private void AfterOpened()
        {
            LayoutDirty = true;
            Relayout();
            registry.NotifyOpened(this);
            if (OnOpen != null)
            {
                Action<IUIMenu> cb = OnOpen;
                Consumer.Invoke(Id, "OnOpen", () => cb(this));
            }
        }

        public void Close()
        {
            MenuHost? host = Host;
            if (host == null)
            {
                return;
            }
            // exitThisMenu → cleanupBeforeExit → OnHostClosed
            host.exitThisMenu(playSound: false);
            if (Host == host)
            {
                OnHostClosed(host);
            }
        }

        /// <summary>Called by the host when the game tears it down (close button, Escape, emergency shutdown) or by the registry when it vanished.</summary>
        internal void OnHostClosed(MenuHost host)
        {
            if (Host != host)
            {
                return;
            }

            Host = null;
            Overlay.CloseAll();
            Overlay.DiscardFrame();
            Focus.ClearFocus();
            Focus.Release();
            Hovered = null;
            registry.NotifyClosed(this);
            if (OnClose != null)
            {
                Action<IUIMenu> cb = OnClose;
                Consumer.Invoke(Id, "OnClose", () => cb(this));
            }
        }

        public override string ToString() => $"UIMenu('{Id}' of {Consumer.ModId})";
    }
}
