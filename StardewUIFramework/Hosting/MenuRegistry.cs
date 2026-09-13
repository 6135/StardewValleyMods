using System.Collections.Generic;
using StardewModdingAPI.Utilities;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// Tracks every menu of every consumer (namespaced by consumer id) and which of them are open on the current
    /// screen, so the framework can close them all on return-to-title and detect menus the game dropped.
    /// </summary>
    internal sealed class MenuRegistry
    {
        private readonly Dictionary<string, Dictionary<string, UIMenu>> menusByConsumer = new();
        // open menus are per screen (split-screen multiplayer)
        private readonly PerScreen<List<UIMenu>> openPerScreen = new(() => new List<UIMenu>());

        private List<UIMenu> open => openPerScreen.Value;

        public IReadOnlyList<UIMenu> OpenMenus => open;

        public UIMenu? Get(string consumerId, string menuId)
        {
            return menusByConsumer.TryGetValue(consumerId, out var menus) && menus.TryGetValue(menuId, out UIMenu? menu) ? menu : null;
        }

        public void Register(string consumerId, UIMenu menu)
        {
            if (!menusByConsumer.TryGetValue(consumerId, out var menus))
                menusByConsumer[consumerId] = menus = new Dictionary<string, UIMenu>();
            menus[menu.Id] = menu;
        }

        public void Unregister(string consumerId, string menuId)
        {
            if (menusByConsumer.TryGetValue(consumerId, out var menus) && menus.Remove(menuId, out UIMenu? menu))
                menu.Close();
        }

        public IEnumerable<UIMenu> MenusOf(string consumerId)
        {
            return menusByConsumer.TryGetValue(consumerId, out var menus) ? menus.Values : System.Array.Empty<UIMenu>();
        }

        internal void NotifyOpened(UIMenu menu)
        {
            if (!open.Contains(menu))
                open.Add(menu);
        }

        internal void NotifyClosed(UIMenu menu) => open.Remove(menu);

        /// <summary>Close menus whose host the game dropped without telling us (another mod replaced the active menu, etc.).</summary>
        public void ValidateOpenMenus()
        {
            for (int i = open.Count - 1; i >= 0; i--)
            {
                UIMenu menu = open[i];
                MenuHost? host = menu.Host;
                if (host == null)
                    open.RemoveAt(i);
                else if (!host.IsStillActive())
                    host.NotifyDropped();
            }
        }

        public void CloseAll()
        {
            foreach (UIMenu menu in open.ToArray())
                menu.Close();
            open.Clear();
        }
    }
}
