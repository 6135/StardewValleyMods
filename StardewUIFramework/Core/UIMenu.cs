using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
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
    /// <para>
    /// The model (tree, options, callbacks) is shared by every split-screen player; what one player sees of it is
    /// per screen (<see cref="ScreenView"/>): the host (so <see cref="IsOpen"/>, <see cref="Open"/> and
    /// <see cref="Close"/> act on the current screen), hover, cursor, focus, overlay, mouse capture, the settled
    /// position and the collapsed state. The shared tree is laid out again whenever another screen uses it.
    /// </para>
    /// </summary>
    internal sealed class UIMenu : IUIMenu
    {
        // chrome insets when DrawBox is on (vanilla dialogue box border + breathing room)
        private const int BoxInsetSide = 56; // IClickableMenu.spaceToClearSideBorder + borderWidth
        private const int BoxInsetTop = 56;
        private const int BoxInsetBottom = 56;
        private const int TitleReserve = 80; // the title scroll is 72 px tall and drawn 64 px above the box

        /// <summary>Width the title scroll adds around its text (<c>SpriteText</c>'s 12 px end caps at 4x, one per side).</summary>
        private const int TitleScrollCaps = 2 * 12 * 4;

        /// <summary>Room kept free on each side between the title (scroll) and the menu's edge.</summary>
        private const int TitleMargin = 8;

        /// <summary>One split-screen player's view of the menu.</summary>
        private sealed class ScreenView
        {
            internal readonly FocusManager Focus;
            internal readonly OverlayLayer Overlay;
            internal readonly EventRouter Router;
            internal MenuHost? Host;
            internal UIElement? Hovered;
            internal UIElement? AnnouncedHover;
            internal double HoverStartMs;
            internal int CursorX, CursorY;
            internal bool Collapsed;

            /// <summary>Position of the first layout since the menu opened on this screen (see <see cref="ResolvePosition"/>).</summary>
            internal Point? Settled;

            internal ScreenView(UIMenu menu)
            {
                Focus = new FocusManager(menu);
                Overlay = new OverlayLayer();
                Router = new EventRouter(menu);
            }

            /// <summary>Forget <paramref name="element"/> (detached from the tree) without raising input events: another screen's view.</summary>
            internal void Forget(UIElement element)
            {
                if (Focus.Focused == element)
                {
                    Focus.ClearFocus();
                }

                if (Hovered == element)
                {
                    Hovered = null;
                }

                if (AnnouncedHover == element)
                {
                    AnnouncedHover = null;
                }

                Router.DropCapture(element);
                Overlay.RemovePopup(element);
            }
        }

        private readonly MenuRegistry registry;
        private readonly PerScreen<ScreenView> screens;
        private int? width, height;
        private UIAnchor anchor = UIAnchor.Center;
        private int x, y, padding;
        private bool drawBox = true;
        private bool showCloseButton = true;
        private Point anchorOffset;

        // the shared tree is arranged for one screen and one theme version at a time
        private bool layoutDirty = true;
        private int layoutScreen = -1;
        private int layoutThemeVersion = -1;

        // reused every tick by RunDataRefresh (a nested refresh takes its own copy)
        private readonly List<Action<bool>> extensionBuffer = new();
        private bool refreshingExtensions;

        /// <summary>Whether the anchor centers the menu horizontally (that axis keeps its first position while open).</summary>
        private bool CentersX => anchor is UIAnchor.Center or UIAnchor.TopCenter or UIAnchor.BottomCenter;

        /// <summary>Whether the anchor centers the menu vertically (that axis keeps its first position while open).</summary>
        private bool CentersY => anchor is UIAnchor.Center or UIAnchor.MiddleLeft or UIAnchor.MiddleRight;
        private Func<string>? title;

        // the title as last fitted by FittedTitle, keyed by the source text, width budget and draw path
        private string? fittedTitleSource;
        private int fittedTitleBudget = -1;
        private bool fittedTitleScroll;
        private string fittedTitle = string.Empty;
        private float fittedTitleWidth;

        internal UIMenu(string id, ConsumerContext consumer, MenuRegistry registry)
        {
            Id = id;
            Consumer = consumer;
            this.registry = registry;
            screens = new PerScreen<ScreenView>(() => new ScreenView(this));
            Root = new Stack(id + ".root", horizontal: false, spacing: 8)
            {
                HorizontalAlign = UIAlign.Stretch,
                VerticalAlign = UIAlign.Stretch
            };
            Viewport = new ScrollView(id + ".viewport", 0)
            {
                FitContent = true,
                HorizontalAlign = UIAlign.Stretch,
                VerticalAlign = UIAlign.Stretch
            };
            Viewport.Add(Root);
            Viewport.SetOwnerMenu(this);
        }

        /// <summary>
        /// The element that hosts <see cref="Root"/>: a fit-content <see cref="ScrollView"/> that is invisible while the
        /// content fits and scrolls it (scrollbar, wheel, clipping) when the window cannot be tall enough. It is part of
        /// the tree (inspector, dumps, hit-testing) but not exposed as the root's parent through the API.
        /// </summary>
        internal ScrollView Viewport { get; }

        // ---------------------------------------------------------------------------------------------------------
        //  Identity / services
        // ---------------------------------------------------------------------------------------------------------

        public string Id { get; }
        internal ConsumerContext Consumer { get; }
        internal Stack Root { get; }

        /// <summary>The current screen's view.</summary>
        private ScreenView View => screens.Value;

        /// <summary>Keyboard focus on the current screen.</summary>
        internal FocusManager Focus => View.Focus;

        /// <summary>Popups and overlay draws on the current screen.</summary>
        internal OverlayLayer Overlay => View.Overlay;

        /// <summary>Input routing and mouse capture on the current screen.</summary>
        internal EventRouter Router => View.Router;

        /// <summary>The game-facing menu while open on the current screen.</summary>
        internal MenuHost? Host => View.Host;

        IUIStack IUIMenu.Root => Root;

        /// <summary>Whether the menu is open on the current screen.</summary>
        public bool IsOpen => View.Host != null;

        /// <summary>Whether the menu is open on any split-screen player's screen.</summary>
        private bool IsOpenOnAnyScreen
        {
            get
            {
                foreach (KeyValuePair<int, ScreenView> pair in screens.GetActiveValues())
                {
                    if (pair.Value.Host != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Times the menu was opened (on any screen); counted before the open-time data refresh runs.</summary>
        internal int OpenCount { get; private set; }

        /// <summary>Times the tree was rebuilt in place (<see cref="RebuildInPlace"/>); counted before the tree is rebuilt.</summary>
        internal int RebuildCount { get; private set; }

        /// <summary>Element under the cursor on the current screen (maintained by the router).</summary>
        internal UIElement? Hovered
        {
            get => View.Hovered;
            set => View.Hovered = value;
        }

        /// <summary>When the cursor entered <see cref="Hovered"/>.</summary>
        internal double HoverStartMs
        {
            get => View.HoverStartMs;
            set => View.HoverStartMs = value;
        }

        internal int CursorX
        {
            get => View.CursorX;
            set => View.CursorX = value;
        }

        internal int CursorY
        {
            get => View.CursorY;
            set => View.CursorY = value;
        }

        /// <summary>Whether the tree must be laid out again: marked dirty, last laid out for another screen, or the theme changed since.</summary>
        internal bool LayoutDirty => layoutDirty || layoutScreen != Context.ScreenId || layoutThemeVersion != Theme.Version;

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
                ResetPosition();
            }
        }

        public int X
        {
            get => x;
            set
            {
                x = value;
                ResetPosition();
            }
        }

        public int Y
        {
            get => y;
            set
            {
                y = value;
                ResetPosition();
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

        // HUD: player-owned layout
        public bool PlayerLayout { get; set; } = true;

        public bool Resizable { get; set; }

        /// <summary>Collapsed by the player (current screen): only the window's title strip is drawn and the tree takes no input.</summary>
        internal bool Collapsed
        {
            get => View.Collapsed;
            set
            {
                if (View.Collapsed == value)
                {
                    return;
                }

                View.Collapsed = value;
                Focus.ClearFocus();
                Overlay.CloseAll();
                MarkLayoutDirty();
            }
        }

        /// <summary>Offset added to the anchor position for non-explicit anchors (HUD widgets).</summary>
        internal Point AnchorOffset
        {
            get => anchorOffset;
            set
            {
                anchorOffset = value;
                ResetPosition();
            }
        }

        /// <summary>The consumer's own placement, captured before a player layout was applied (null = none applied yet).</summary>
        internal WindowLayout? ConsumerLayout { get; set; }

        /// <summary>Whether the player may resize the window: both dimensions are fixed, or the consumer opted in with <see cref="Resizable"/>.</summary>
        internal bool IsResizable => Resizable || (width.HasValue && height.HasValue);

        /// <summary>
        /// Smallest size the player may resize the window to: the narrowest width its content can take without
        /// overflowing (<see cref="UIElement.MeasureMinWidth"/>) but never less than <paramref name="chromeWidth"/>, and
        /// enough height for the viewport to scroll a few rows. A pure query: nothing is re-measured or re-arranged.
        /// </summary>
        /// <param name="chromeWidth">Width the window's own controls need (collapse / close buttons, resize grip).</param>
        internal Point MinimumSize(int chromeWidth)
        {
            int contentW = (int)Math.Ceiling(Viewport.MeasureMinWidth());
            int contentH = Math.Min((int)Math.Ceiling(Viewport.DesiredSize.Y), MinimumViewportHeight);
            return new Point(
                Math.Max(contentW + InsetLeft + InsetRight, chromeWidth),
                contentH + InsetTop + InsetBottom);
        }

        /// <summary>Height below which a resized window is not useful (three rows + scrollbar arrows).</summary>
        private const int MinimumViewportHeight = 160;

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

        // BEGIN DATA menu
        /// <summary>
        /// Refresh hook of data-driven menus (v1.3): re-applies dynamic values and evaluates open-time conditions. Runs
        /// at the top of <see cref="Tick"/> (<c>opening</c> = false) and when the menu opens, before slots and
        /// decorators are rebuilt (<c>opening</c> = true). Null for C# menus.
        /// </summary>
        internal Action<UIMenu, bool>? DataRefresh { get; set; }

        /// <summary>
        /// Refreshers of data built into this menu by others (v1.7): data composite bodies, data slot contributions and
        /// decorations, keyed by what registered them. Run right after <see cref="DataRefresh"/>, in C# menus too.
        /// </summary>
        internal Dictionary<object, Action<bool>> ExtensionRefresh { get; } = new();
        // END DATA menu

        public void SetPosition(int px, int py)
        {
            x = px;
            y = py;
            anchor = UIAnchor.Explicit;
            ResetPosition();
        }

        /// <summary>
        /// Place the menu from its anchor again on the next layout (the position was changed, the window was resized, or
        /// the menu reopens). While open, a centered axis otherwise keeps the position of the first layout (see
        /// <see cref="ResolvePosition"/>).
        /// </summary>
        internal void ResetPosition()
        {
            foreach (KeyValuePair<int, ScreenView> pair in screens.GetActiveValues())
            {
                pair.Value.Settled = null;
            }

            MarkLayoutDirty();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Tree
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Set while a slot contribution or decorator of another mod runs against this menu, so <see cref="Find"/>
        /// hides sealed subtrees from it (see <see cref="Sealing"/>).
        /// </summary>
        internal ConsumerContext? ExternalConsumer { get; set; }

        public IUIElement Find(string id) => (ExternalConsumer == null ? Root.FindById(id) : Sealing.FindReachable(Root, id, ExternalConsumer))!;

        internal void OnElementDetached(UIElement element)
        {
            int screen = Context.ScreenId;
            foreach (KeyValuePair<int, ScreenView> pair in screens.GetActiveValues())
            {
                if (pair.Key != screen)
                {
                    pair.Value.Forget(element);
                }
            }

            if (Focus.Focused == element)
            {
                Focus.ClearFocus();
            }

            if (Hovered == element)
            {
                Hovered = null;
            }

            if (View.AnnouncedHover == element)
            {
                View.AnnouncedHover = null;
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

            element.BindingOwner?.Drop(element); // SIGNALS: bindings made by a contributor or composite
            Consumer.Bindings.Drop(element);
        }

        public void InvalidateLayout() => MarkLayoutDirty();

        internal void MarkLayoutDirty() => layoutDirty = true;

        // ---------------------------------------------------------------------------------------------------------
        //  Layout
        // ---------------------------------------------------------------------------------------------------------

        private int InsetLeft => (drawBox ? BoxInsetSide : 0) + Theme.Space(padding);
        private int InsetRight => (drawBox ? BoxInsetSide : 0) + Theme.Space(padding);
        private int InsetTop => (drawBox ? BoxInsetTop : 0) + Theme.Space(padding);
        private int InsetBottom => (drawBox ? BoxInsetBottom : 0) + Theme.Space(padding);

        /// <summary>Measure the root, compute the menu rectangle from the size / position policy, arrange the tree.</summary>
        internal void Relayout()
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Layout);
            Point vp = UIServices.ViewportSize();
            int insetW = InsetLeft + InsetRight;
            int insetH = InsetTop + InsetBottom;

            // the title banner sits above the box, so a tall window must leave room for it
            int maxH = Math.Max(1, vp.Y - (title != null && drawBox ? TitleReserve : 0));
            float availW = (width ?? vp.X) - insetW;
            float availH = Math.Min(height ?? maxH, maxH) - insetH;
            Viewport.Measure(new Vector2(Math.Max(0, availW), Math.Max(0, availH)));

            ScreenView view = View;
            int w = width ?? (int)Math.Ceiling(Viewport.DesiredSize.X) + insetW;
            int h = view.Collapsed ? insetH : height ?? (int)Math.Ceiling(Viewport.DesiredSize.Y) + insetH;
            w = Math.Clamp(w, Math.Min(insetW, vp.X), Math.Max(vp.X, 1));
            h = Math.Clamp(h, Math.Min(insetH, maxH), maxH);

            Point position = ResolvePosition(vp, w, h);
            if (view.Host != null)
            {
                view.Settled ??= position; // the first layout since the menu opened
            }

            Bounds = new Rectangle(position.X, position.Y, w, h);
            if (!view.Collapsed)
            {
                // a collapsed window keeps the last arrangement; it is re-arranged when expanded
                Viewport.Arrange(new Rectangle(position.X + InsetLeft, position.Y + InsetTop, Math.Max(0, w - insetW), Math.Max(0, h - insetH)));
            }
            layoutDirty = false;
            layoutScreen = Context.ScreenId;
            layoutThemeVersion = Theme.Version;

            view.Host?.SyncBounds();
            Focus.Validate();
        }

        /// <summary>Top-left corner for a menu of the given size: the anchor (or explicit X/Y), clamped to the viewport and below the title banner.</summary>
        /// <remarks>
        /// While the menu is open, an axis the anchor centers keeps the position of the first layout, so content that
        /// grows or shrinks (a line shown on hover, a tab switch) extends the window from where it is instead of
        /// re-centering it and moving everything under the cursor. Edge anchors are stable already.
        /// </remarks>
        private Point ResolvePosition(Point vp, int w, int h)
        {
            int minY = title != null && drawBox ? Math.Min(TitleReserve, Math.Max(0, vp.Y - h)) : 0;
            Point? settled = View.Settled;
            int px = anchor == UIAnchor.Explicit ? x
                : settled.HasValue && CentersX ? settled.Value.X
                : AnchorX(vp.X, w) + anchorOffset.X;
            int py = anchor == UIAnchor.Explicit ? y
                : settled.HasValue && CentersY ? settled.Value.Y
                : AnchorY(vp.Y, h, minY) + anchorOffset.Y;
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

        /// <summary>The title banner plus the top border of the box: the strip the player drags the window by (HUD).</summary>
        internal Rectangle TitleStrip
        {
            get
            {
                int banner = title != null && drawBox ? TitleReserve : 0;
                return new Rectangle(Bounds.X, Bounds.Y - banner, Bounds.Width, banner + InsetTop);
            }
        }

        /// <summary>Absolute content rectangle (inside chrome and padding).</summary>
        internal Rectangle ContentBounds => new(Bounds.X + InsetLeft, Bounds.Y + InsetTop, Math.Max(0, Bounds.Width - InsetLeft - InsetRight), Math.Max(0, Bounds.Height - InsetTop - InsetBottom));

        // ---------------------------------------------------------------------------------------------------------
        //  Per-frame
        // ---------------------------------------------------------------------------------------------------------

        internal void Tick(double elapsedMs)
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Update);
            RunDataRefresh(opening: false); // DATA
            if (LayoutDirty)
            {
                Relayout();
            }

            Focus.Validate();
            Root.Update(elapsedMs);
            AnnounceRestingHover();
            if (OnUpdate != null)
            {
                Action<IUIMenu, double> cb = OnUpdate;
                Consumer.InvokeWith(Id, Id, "OnUpdate", static s => s.cb(s.menu, s.elapsedMs), (cb, menu: (IUIMenu)this, elapsedMs));
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
                DrawChrome(b);
            }

            string? titleText = title == null ? null : Pseudo.Transform(Consumer.Invoke(Id, Id, "Title", title, string.Empty));
            if (!string.IsNullOrEmpty(titleText))
            {
                if (drawBox)
                {
                    // the scroll graphic spans [y - 12, y + 60]; keep it just above the frame, never wider than it
                    string shown = FittedTitle(titleText, Bounds.Width - (2 * TitleMargin) - TitleScrollCaps, scroll: true);
                    SpriteText.drawStringWithScrollCenteredAt(b, shown, Bounds.Center.X, Math.Max(12, Bounds.Y - 68));
                }
                else
                {
                    string shown = FittedTitle(titleText, Bounds.Width - (2 * TitleMargin), scroll: false);
                    DrawHelper.Text(b, shown, UIFont.Dialogue, new Vector2(Bounds.Center.X - (fittedTitleWidth / 2f), Bounds.Y + 8), Theme.TextColor, false, 1f);
                }
            }

            if (View.Collapsed)
            {
                Overlay.DiscardFrame();
            }
            else
            {
                Viewport.Draw(b);
                InspectorRenderer.Draw(this, b);
                Overlay.Draw(b);
                DrawTooltip(b);
            }

            if (UIServices.Config.DebugOverlay)
            {
                DrawHelper.DebugBounds(b, Bounds, Id, Color.Red);
            }
        }

        /// <summary>
        /// <paramref name="text"/> as the title can show it in <paramref name="budget"/> pixels: whole when it fits,
        /// otherwise its longest prefix + "..." (just "..." when even one character does not fit). Measured with
        /// <c>SpriteText</c> for the scroll banner (<paramref name="scroll"/>) or the dialogue font otherwise; the result
        /// and its width (<see cref="fittedTitleWidth"/>) are cached until the text, budget or path changes.
        /// </summary>
        private string FittedTitle(string text, int budget, bool scroll)
        {
            if (text == fittedTitleSource && budget == fittedTitleBudget && scroll == fittedTitleScroll)
            {
                return fittedTitle;
            }

            fittedTitleSource = text;
            fittedTitleBudget = budget;
            fittedTitleScroll = scroll;
            fittedTitle = text;
            if (TitleWidth(text, scroll) > budget)
            {
                const string Ellipsis = "...";
                int lo = 0, hi = text.Length;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (TitleWidth(text.Substring(0, mid).TrimEnd() + Ellipsis, scroll) <= budget)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
                fittedTitle = text.Substring(0, lo).TrimEnd() + Ellipsis;
            }

            fittedTitleWidth = TitleWidth(fittedTitle, scroll);
            return fittedTitle;
        }

        /// <summary>Drawn width of a title string: <c>SpriteText</c> for the scroll banner, the dialogue font otherwise.</summary>
        private static float TitleWidth(string text, bool scroll) => scroll ? SpriteText.getWidthOfString(text) : UIServices.Text.Measure(UIFont.Dialogue, text, 1f).X;

        /// <summary>The vanilla dialogue box, or the theme's panel box when the theme restyles boxes (tint, texture or solid fill).</summary>
        private void DrawChrome(SpriteBatch b)
        {
            if (Theme.IsVanillaChrome)
            {
                // drawDialogueBox draws its frame 64 px below the y it is given (and 64 px shorter), so offset the call
                // to make the visible frame exactly Bounds
                Game1.drawDialogueBox(Bounds.X, Bounds.Y - 64, Bounds.Width, Bounds.Height + 64, speaker: false, drawOnlyBox: true);
                return;
            }

            DrawHelper.ThemedBox(b, Theme.PanelTexture, Theme.PanelBoxSource, Bounds, Color.White, 1f);
        }

        /// <summary>Screen reader: describe the hovered element once the cursor rested on it for the tooltip delay.</summary>
        private void AnnounceRestingHover()
        {
            ScreenView view = View;
            UIElement? hovered = view.Hovered;
            if (hovered == null || hovered == view.AnnouncedHover || !Accessibility.Enabled)
            {
                view.AnnouncedHover = hovered;
                return;
            }

            if (UIServices.NowMs() - HoverStartMs < Consumer.EffectiveTooltipDelay)
            {
                return;
            }

            view.AnnouncedHover = hovered;
            if (!hovered.IsFocused)
            {
                Accessibility.AnnounceElement(hovered);
            }
        }

        /// <summary>
        /// Draw the tooltip of the hovered element once the delay elapsed (also used by HUD widgets). An element without
        /// a tooltip shows its nearest ancestor's, so the parts of a row, cell or composite share the tooltip set on it
        /// (an image inside a data grid row shows the row's tooltip).
        /// </summary>
        internal void DrawTooltip(SpriteBatch b)
        {
            UIElement? hovered = TooltipOwner(Hovered);
            if (hovered == null || Overlay.HasPopups)
            {
                return;
            }

            if (UIServices.NowMs() - HoverStartMs < Consumer.EffectiveTooltipDelay)
            {
                return;
            }

            if (hovered.RichTooltip != null)
            {
                TooltipRenderer.Draw(b, this, hovered, hovered.RichTooltip);
                return;
            }

            // the element's own guard (a slot contributor for contributed elements), not the menu owner's
            ConsumerContext guard = hovered.Consumer;
            string text = Pseudo.Transform(guard.Invoke(Id, hovered.Id, "Tooltip", hovered.Tooltip, string.Empty));
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string? tooltipTitle = hovered.TooltipTitle == null ? null : Pseudo.Transform(guard.Invoke(Id, hovered.Id, "TooltipTitle", hovered.TooltipTitle, string.Empty));
            IClickableMenu.drawHoverText(b, text, Game1.smallFont, boldTitleText: string.IsNullOrEmpty(tooltipTitle) ? null : tooltipTitle);
        }

        /// <summary>The element whose tooltip applies to <paramref name="element"/>: itself, else its nearest ancestor with one.</summary>
        internal static UIElement? TooltipOwner(UIElement? element)
        {
            for (UIElement? e = element; e != null; e = e.ParentElement)
            {
                if (e.Tooltip != null || e.RichTooltip != null)
                {
                    return e;
                }
            }

            return null;
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
            var host = new MenuHost(this);
            View.Host = host;
            Game1.activeClickableMenu = host;
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
            var host = new MenuHost(this);
            View.Host = host;
            parent.Host.SetChildMenu(host);
            AfterOpened();
        }

        private void AfterOpened()
        {
            OpenCount++;
            RunDataRefresh(opening: true); // DATA
            registry.NotifyOpening(this);
            layoutDirty = true;
            Relayout();
            registry.NotifyOpened(this);
            View.AnnouncedHover = null;
            if (Accessibility.Enabled)
            {
                string titleText = title == null ? string.Empty : Consumer.Invoke(Id, Id, "Title", title, string.Empty) ?? string.Empty;
                Accessibility.Announce(Accessibility.Compose(Accessibility.Text("menu", "Menu"), titleText.Length > 0 ? titleText : Id));
            }
            if (OnOpen != null)
            {
                Action<IUIMenu> cb = OnOpen;
                Consumer.Invoke(Id, Id, "OnOpen", () => cb(this));
            }

            Focus.UpdateSubscription(); // an input focused before the menu opened takes the keyboard now
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
            ScreenView view = View;
            if (view.Host != host)
            {
                return;
            }

            view.Host = null;
            view.Settled = null;
            Overlay.CloseAll();
            Overlay.DiscardFrame();
            Focus.ClearFocus();
            Focus.Release();
            Hovered = null;
            registry.NotifyClosed(this);
            if (OnClose != null)
            {
                Action<IUIMenu> cb = OnClose;
                Consumer.Invoke(Id, Id, "OnClose", () => cb(this));
            }
        }

        // BEGIN DATA rebuild

        /// <summary>
        /// Rebuild the tree without replacing the menu: the same <see cref="UIMenu"/> and <see cref="MenuHost"/> stay, so
        /// an open menu stays open, child menus survive and references to the menu stay valid (data hot reload).
        /// <list type="number">
        ///   <item>capture the view state by element id (focus, scroll offsets, list / grid position, sort, selection, column widths);</item>
        ///   <item>close the overlay and clear focus and hover;</item>
        ///   <item>clear the root (drops bindings through <see cref="OnElementDetached"/>);</item>
        ///   <item>run <paramref name="build"/> (re-applies the options and builds the new tree);</item>
        ///   <item>if open: run the data refresh and <see cref="MenuRegistry.NotifyOpening"/> (slots and decorators) and lay out;</item>
        ///   <item>restore the view state and forget muted callbacks.</item>
        /// </list>
        /// </summary>
        internal void RebuildInPlace(Action<UIMenu> build)
        {
            ArgumentNullException.ThrowIfNull(build);

            MenuViewState view = MenuViewState.Capture(this);
            Overlay.CloseAll();
            Focus.ClearFocus();
            Hovered = null;
            View.AnnouncedHover = null;
            RebuildCount++;
            Root.Clear();

            build(this);

            if (IsOpenOnAnyScreen)
            {
                RunDataRefresh(opening: true);
                registry.NotifyOpening(this);
            }

            layoutDirty = true;
            Relayout();
            view.Restore(this);
            Consumer.ResetMutes(Id);
        }

        /// <summary>Run <see cref="DataRefresh"/> inside the owner's callback guard.</summary>
        private void RunDataRefresh(bool opening)
        {
            Action<UIMenu, bool>? refresh = DataRefresh;
            if (refresh != null)
            {
                Consumer.InvokeWith(Id, Id, "DataRefresh", static s => s.refresh(s.menu, s.opening), (refresh, menu: this, opening));
            }

            if (ExtensionRefresh.Count == 0)
            {
                return;
            }

            // run over a snapshot, since a refresher may change the table; each entry isolates its own failures
            // (refresher groups log and skip a faulting value)
            bool outer = !refreshingExtensions;
            List<Action<bool>> snapshot = outer ? extensionBuffer : new List<Action<bool>>();
            snapshot.Clear();
            snapshot.AddRange(ExtensionRefresh.Values);
            refreshingExtensions = true;
            try
            {
                for (int i = 0; i < snapshot.Count; i++)
                {
                    snapshot[i](opening);
                }
            }
            finally
            {
                if (outer)
                {
                    refreshingExtensions = false;
                    snapshot.Clear();
                }
            }
        }

        // END DATA rebuild

        public override string ToString() => $"UIMenu('{Id}' of {Consumer.ModId})";
    }
}
