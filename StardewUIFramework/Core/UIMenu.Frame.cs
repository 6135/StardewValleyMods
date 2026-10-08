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
    internal sealed partial class UIMenu
    {
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
            Focus.Tick();
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

        /// <summary>Draw the window and its content; the host then draws its window buttons and <see cref="DrawTop"/>.</summary>
        internal void Draw(SpriteBatch b)
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Draw);
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

            if (!View.Collapsed)
            {
                Viewport.Draw(b);
            }
        }

        /// <summary>Draw what floats above the window and its buttons: popups, the tooltip and the inspector.</summary>
        internal void DrawTop(SpriteBatch b)
        {
            using PerfCounters.Scope perf = PerfCounters.Begin(this, PerfCounters.Phase.Draw, endsFrame: true);
            if (View.Collapsed)
            {
                Overlay.DiscardFrame();
            }
            else
            {
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
    }
}
