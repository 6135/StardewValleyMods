using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Changes the active theme at runtime (API <c>SetTheme</c>, the <c>ui_theme</c> console command, GMCM): writes
    /// the config value, persists it and drops the cached theme; that bumps <see cref="Theme.Version"/>, so every menu
    /// and HUD widget (on every screen) lays out again and spacing / font scale changes apply immediately.
    /// </summary>
    internal static class ThemeSwitcher
    {
        /// <summary>
        /// Provide the bundled <c>assets/themes.json</c> as the theme asset (Content Patcher packs edit it) and drop the
        /// cached theme when the asset is invalidated. <c>AssetReady</c> is not needed: the theme reloads lazily on the
        /// next read after an invalidation.
        /// </summary>
        internal static void Register(IContentEvents content)
        {
            content.AssetRequested += OnAssetRequested;
            content.AssetsInvalidated += (_, e) =>
            {
                if (e.NamesWithoutLocale.Any(n => n.IsEquivalentTo(Theme.AssetName)))
                {
                    Refresh();
                }
            };
        }

        private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(Theme.AssetName))
            {
                e.LoadFromModFile<Dictionary<string, ThemeData>>("assets/themes.json", AssetLoadPriority.Exclusive);
            }
        }

        /// <summary>The theme names for a settings dropdown: every theme in the asset plus the configured name (so an unknown value still shows).</summary>
        internal static string[] Choices()
        {
            var names = new List<string>(Theme.ThemeNames);
            string configured = UIServices.Config.Theme;
            if (!names.Contains(configured, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(configured);
            }

            return names.ToArray();
        }

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

        /// <summary>Drop cached theme data (asset invalidated, config reset, scale changed); menus re-layout through <see cref="Theme.Version"/>.</summary>
        internal static void Refresh() => Theme.Invalidate();
    }
}
