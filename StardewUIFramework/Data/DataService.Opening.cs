using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Data
{
    /// <summary>Opening / closing data and C# menus (trigger actions, tile actions, console), with the wait-until-free queue.</summary>
    internal sealed partial class DataService
    {
        /// <summary>The menu registered as <c>owner/menu</c> (data or C#), or null.</summary>
        internal UIMenu? Find(string key)
        {
            return DataValidator.TrySplitKey(key ?? string.Empty, out string owner, out string id) ? menus.Get(owner, id) : null;
        }

        /// <summary>The topmost open framework menu on this screen, or null.</summary>
        internal UIMenu? Topmost => menus.OpenMenus.Count > 0 ? menus.OpenMenus[^1] : null;

        /// <summary>Re-evaluate a menu's open-time values (<c>Condition</c>s, one-time values) and lay it out again (<c>_Refresh</c>).</summary>
        internal void RefreshMenu(UIMenu menu)
        {
            RuntimeOf(menu)?.Refresh(menu, opening: true);
            menu.InvalidateLayout();
        }

        /// <summary>The data runtime whose tree lives in <paramref name="menu"/> (a data menu, or a data HUD's inner menu), or null.</summary>
        internal DataRuntime? RuntimeOf(UIMenu menu)
        {
            return built.Values.FirstOrDefault(r => r.Menu == menu) ?? (DataRuntime?)huds.Values.FirstOrDefault(h => h.Model == menu);
        }

        /// <summary>Open a menu; without <paramref name="force"/> it waits until the player is free. False (with an error) when it cannot.</summary>
        internal bool Open(string key, bool force, out string error)
        {
            if (!TryGetOpenable(key, out UIMenu? menu, out error))
            {
                return false;
            }

            if (menu.IsOpen)
            {
                return true;
            }

            if (!force && !Context.IsPlayerFree)
            {
                Queue(key);
                return true;
            }

            menu.Open(true);
            return true;
        }

        /// <summary>Open a menu as a child of <paramref name="parentKey"/> (default: the topmost open framework menu).</summary>
        internal bool OpenAsChild(string key, string? parentKey, out string error)
        {
            if (!TryGetOpenable(key, out UIMenu? menu, out error))
            {
                return false;
            }

            UIMenu? parent = string.IsNullOrWhiteSpace(parentKey) ? Topmost : Find(parentKey);
            if (parent == null || !parent.IsOpen)
            {
                error = string.IsNullOrWhiteSpace(parentKey) ? "no framework menu is open to be the parent." : $"the parent menu '{parentKey}' is not open.";
                return false;
            }

            if (parent == menu)
            {
                error = "a menu cannot be its own parent.";
                return false;
            }

            menu.OpenAsChild(parent);
            return true;
        }

        /// <summary>Close a menu (default: the topmost open framework menu).</summary>
        internal bool Close(string? key, out string error)
        {
            error = string.Empty;
            UIMenu? menu = string.IsNullOrWhiteSpace(key) ? Topmost : Find(key);
            if (menu == null)
            {
                error = string.IsNullOrWhiteSpace(key) ? "no framework menu is open." : $"no menu '{key}' is registered.";
                return false;
            }

            menu.Close();
            return true;
        }

        /// <summary>Close the menu when open, else open it (waiting until the player is free).</summary>
        internal bool Toggle(string key, out string error)
        {
            UIMenu? menu = Find(key);
            if (menu != null && menu.IsOpen)
            {
                error = string.Empty;
                menu.CloseByPlayer();
                return true;
            }

            return Open(key, force: false, out error);
        }

        /// <summary>True when the menu is open on this screen.</summary>
        internal bool IsOpen(string key) => Find(key)?.IsOpen ?? false;

        private bool TryGetOpenable(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UIMenu? menu, out string error)
        {
            error = string.Empty;
            menu = Find(key);
            if (menu == null)
            {
                error = $"no menu '{key}' is registered (expected '<owner>/<menu id>').";
                return false;
            }

            if (built.TryGetValue(key, out DataMenuRuntime? runtime) && runtime.Menu == menu && !DataActionRunner.CheckCondition(runtime.Definition.Condition))
            {
                error = $"the Condition of menu '{key}' does not match.";
                return false;
            }

            return true;
        }

        private void Queue(string key)
        {
            List<string> queue = pendingOpens.Value;
            if (!queue.Contains(key))
            {
                queue.Add(key);
                monitor.Log($"Menu '{key}' will open when the player is free.", LogLevel.Trace);
            }
        }

        private void ProcessPendingOpens()
        {
            List<string> queue = pendingOpens.Value;
            if (queue.Count == 0 || !Context.IsPlayerFree)
            {
                return;
            }

            string key = queue[0];
            queue.RemoveAt(0);
            if (!Open(key, force: true, out string error))
            {
                monitor.Log($"Queued menu '{key}' could not open: {error}", LogLevel.Warn);
            }
        }
    }
}
