using System;
using StardewModdingAPI;
using UIFramework.Core;
using UIFramework.Hosting;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Changes the active theme at runtime (API <c>SetTheme</c>, the <c>ui_theme</c> console command, GMCM): writes
    /// the config value, persists it, drops the cached theme and re-lays out every open menu so spacing / font
    /// scale changes apply immediately.
    /// </summary>
    internal static class ThemeSwitcher
    {
        /// <summary>Open menus to re-layout after a change (set by <see cref="ModEntry"/>).</summary>
        internal static MenuRegistry? Menus { get; set; }

        /// <summary>Activate <paramref name="name"/>; an unknown name is logged and falls back to the default theme.</summary>
        internal static void Apply(string name, string requester)
        {
            name = name.Trim();
            string[] known = Theme.ThemeNames;
            string resolved = Array.Find(known, k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase)) ?? Theme.DefaultThemeName;
            if (!string.Equals(resolved, name, StringComparison.OrdinalIgnoreCase))
            {
                UIServices.Log($"[{requester}] theme '{name}' does not exist (known: {string.Join(", ", known)}); using '{resolved}'.", LogLevel.Warn);
            }

            if (string.Equals(UIServices.Config.Theme, resolved, StringComparison.Ordinal))
            {
                return;
            }

            UIServices.Config.Theme = resolved;
            Refresh();
            try
            {
                UIServices.SaveConfig?.Invoke();
            }
            catch (Exception ex)
            {
                UIServices.Log($"Could not save the theme setting:\n{ex}", LogLevel.Warn);
            }
            UIServices.Log($"[{requester}] theme set to '{resolved}'.", LogLevel.Info);
        }

        /// <summary>Drop cached theme data and re-layout open menus (asset invalidated, config reset, scale changed).</summary>
        internal static void Refresh()
        {
            Theme.Invalidate();
            if (Menus == null)
            {
                return;
            }

            foreach (UIMenu menu in Menus.OpenMenus)
            {
                menu.InvalidateLayout();
            }
        }
    }
}
