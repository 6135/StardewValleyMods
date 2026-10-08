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
            Host?.SnapForGamepad();
        }

        /// <summary>Close the menu because the player asked to (a hotkey, Escape, gamepad B...): with the vanilla close sound.</summary>
        internal void CloseByPlayer()
        {
            MenuHost? host = Host;
            if (host == null)
            {
                return;
            }

            host.PlayCloseSound();
            Close();
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
