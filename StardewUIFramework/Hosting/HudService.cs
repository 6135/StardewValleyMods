using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// Owns every HUD widget (namespaced by consumer id) and the per-screen toast stack. Draws widgets from
    /// <see cref="IDisplayEvents.RenderedHud"/> while no menu is open, no event is running and the vanilla HUD is
    /// shown; toasts draw from the same handler, or from <see cref="IDisplayEvents.RenderedActiveMenu"/> while a menu
    /// is up so they stay on top of it. Interactive widgets get hover / clicks / drag from the SMAPI input events.
    /// </summary>
    internal sealed class HudService
    {
        /// <summary>Per-screen input state (split-screen).</summary>
        private sealed class ScreenState
        {
            internal readonly ToastLayer Toasts = new();
            /// <summary>Widget being dragged by the player.</summary>
            internal UIHud? Dragging;
            internal Point DragStart;
            internal Point DragOrigin;
            /// <summary>Widget that received the last left click (gets held / release callbacks).</summary>
            internal UIHud? Pressed;
        }

        private readonly Dictionary<string, UIHud> huds = new();
        private readonly MenuRegistry menus;
        private readonly IInputHelper input;
        private readonly PerScreen<ScreenState> screens = new(() => new ScreenState());

        internal HudService(IModHelper helper, MenuRegistry menus)
        {
            this.menus = menus;
            input = helper.Input;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;
            helper.Events.Input.CursorMoved += OnCursorMoved;
        }

        /// <summary>Whether HUD widgets are drawn / take input right now: in the world, no menu, no event, vanilla HUD shown.</summary>
        private static bool HudsActive => Context.IsWorldReady && Game1.activeClickableMenu == null && !Game1.eventUp && Game1.displayHUD;

        // ---------------------------------------------------------------------------------------------------------
        //  Registry
        // ---------------------------------------------------------------------------------------------------------

        private static string Key(string consumerId, string id) => consumerId + "/" + id;

        internal UIHud Create(ConsumerContext consumer, string id)
        {
            string key = Key(consumer.ModId, id);
            if (huds.ContainsKey(key))
            {
                UIServices.Log($"[{consumer.ModId}] HUD widget '{id}' already exists; it is replaced.", LogLevel.Debug);
                Destroy(consumer.ModId, id);
            }

            var hud = new UIHud(id, consumer, menus);
            huds[key] = hud;
            UIServices.Layouts?.Apply(hud);
            return hud;
        }

        internal UIHud? Get(string consumerId, string id) => huds.TryGetValue(Key(consumerId, id), out UIHud? hud) ? hud : null;

        internal void Destroy(string consumerId, string id)
        {
            if (huds.Remove(Key(consumerId, id), out UIHud? hud))
            {
                hud.ReleaseInput();
                ScreenState state = screens.Value;
                if (state.Dragging == hud)
                {
                    state.Dragging = null;
                }

                if (state.Pressed == hud)
                {
                    state.Pressed = null;
                }
            }
        }

        /// <summary>Re-apply the stored offsets to every widget (after the save's layouts were loaded).</summary>
        internal void ApplyLayouts()
        {
            foreach (UIHud hud in huds.Values)
            {
                UIServices.Layouts?.Apply(hud);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Toasts
        // ---------------------------------------------------------------------------------------------------------

        internal void ShowToast(string text, Texture2D? icon, Rectangle? source, int durationMs)
        {
            screens.Value.Toasts.Add(text, icon, source, durationMs);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Frame
        // ---------------------------------------------------------------------------------------------------------

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            double elapsed = Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 1000.0 / 60;
            ScreenState state = screens.Value;
            state.Toasts.Update(elapsed);

            bool active = HudsActive;
            foreach (UIHud hud in huds.Values)
            {
                if (hud.EvaluateShown() && active)
                {
                    hud.Tick(elapsed);
                }
                else if (hud.Inner.Hovered != null || hud.Inner.Focus.Focused != null || hud.Inner.Overlay.HasPopups)
                {
                    hud.ReleaseInput();
                }
                else
                {
                    // hidden and idle: nothing to do
                }
            }

            if (!active)
            {
                EndDrag(state, persist: false);
                state.Pressed = null;
            }
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (HudsActive)
            {
                foreach (UIHud hud in huds.Values)
                {
                    if (hud.IsShown)
                    {
                        hud.Draw(e.SpriteBatch);
                    }
                }
            }

            // toasts draw here while no menu is open; RenderedActiveMenu takes over while one is (so they stay on top)
            if (Game1.activeClickableMenu == null && !Game1.eventUp)
            {
                screens.Value.Toasts.Draw(e.SpriteBatch);
            }
        }

        private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
        {
            if (!screens.Value.Toasts.IsEmpty)
            {
                screens.Value.Toasts.Draw(e.SpriteBatch);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input (interactive widgets, only while no menu is open)
        // ---------------------------------------------------------------------------------------------------------

        private static Point ToUiPixels(ICursorPosition cursor)
        {
            Vector2 p = cursor.GetScaledScreenPixels();
            return new Point((int)p.X, (int)p.Y);
        }

        /// <summary>Interactive, shown widgets, top-most (last created) first.</summary>
        private List<UIHud> InteractiveHuds()
        {
            var list = new List<UIHud>();
            foreach (UIHud hud in huds.Values)
            {
                if (hud.Interactive && hud.IsShown)
                {
                    list.Insert(0, hud);
                }
            }
            return list;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!HudsActive || (e.Button != SButton.MouseLeft && e.Button != SButton.MouseRight))
            {
                return;
            }

            Point cursor = ToUiPixels(e.Cursor);
            UIMouseButton button = e.Button == SButton.MouseLeft ? UIMouseButton.Left : UIMouseButton.Right;
            foreach (UIHud hud in InteractiveHuds())
            {
                if (!hud.Bounds.Contains(cursor) && !hud.Inner.Overlay.HasPopups)
                {
                    continue;
                }

                if (Click(hud, cursor, button))
                {
                    input.Suppress(e.Button);
                    return;
                }
            }
        }

        /// <summary>Route a click into one widget; an unhandled left click on the widget's box starts a drag. Returns true if the game should lose the click.</summary>
        private bool Click(UIHud hud, Point cursor, UIMouseButton button)
        {
            ScreenState state = screens.Value;
            bool handled = hud.Inner.Router.Click(cursor.X, cursor.Y, button);
            hud.Inner.Focus.ClearFocus(); // HUD widgets never take keyboard focus (there is no host to forward keys)
            if (button != UIMouseButton.Left)
            {
                return handled;
            }

            if (handled)
            {
                state.Pressed = hud;
                return true;
            }

            if (!hud.Bounds.Contains(cursor))
            {
                return false;
            }

            state.Dragging = hud;
            state.DragStart = cursor;
            state.DragOrigin = new Point(hud.X, hud.Y);
            return true;
        }

        private void OnCursorMoved(object? sender, CursorMovedEventArgs e)
        {
            if (!HudsActive)
            {
                return;
            }

            Point cursor = ToUiPixels(e.NewPosition);
            ScreenState state = screens.Value;
            if (state.Dragging != null)
            {
                state.Dragging.SetOffset(state.DragOrigin.X + cursor.X - state.DragStart.X, state.DragOrigin.Y + cursor.Y - state.DragStart.Y);
                return;
            }

            state.Pressed?.Inner.Router.ClickHeld(cursor.X, cursor.Y);
            foreach (UIHud hud in InteractiveHuds())
            {
                hud.Inner.Router.Hover(cursor.X, cursor.Y);
            }
        }

        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            if (e.Button != SButton.MouseLeft)
            {
                return;
            }

            ScreenState state = screens.Value;
            EndDrag(state, persist: true);
            if (state.Pressed != null)
            {
                Point cursor = ToUiPixels(e.Cursor);
                state.Pressed.Inner.Router.ClickReleased(cursor.X, cursor.Y);
                state.Pressed = null;
            }
        }

        private static void EndDrag(ScreenState state, bool persist)
        {
            UIHud? hud = state.Dragging;
            state.Dragging = null;
            if (hud != null && persist)
            {
                UIServices.Layouts?.Remember(hud);
            }
        }
    }
}
