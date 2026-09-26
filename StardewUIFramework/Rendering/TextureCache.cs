using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Texture assets loaded by name through the game content pipeline (so Content Patcher edits apply), cached until
    /// the asset is invalidated. Used by the theme (box / button / text box textures) and the data layer (sprite refs).
    /// A failed load is cached as null so a missing texture is only reported once.
    /// </summary>
    internal static class TextureCache
    {
        private static readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Load a texture asset by name; null when the name is empty or the load fails (logged once, attributed to <paramref name="requester"/>).</summary>
        internal static Texture2D? Load(string? assetName, string requester)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return null;
            }

            string key = Normalize(assetName);
            if (Textures.TryGetValue(key, out Texture2D? cached))
            {
                return cached;
            }

            Texture2D? texture = null;
            try
            {
                texture = UIServices.GameContent?.Load<Texture2D>(assetName.Trim());
            }
            catch (Exception ex)
            {
                UIServices.Log($"{requester} references texture '{assetName}' which could not be loaded.\n{ex}", LogLevel.Warn);
            }
            Textures[key] = texture;
            return texture;
        }

        /// <summary>Forget every cached texture.</summary>
        internal static void Invalidate() => Textures.Clear();

        /// <summary>Forget the cached textures whose asset was invalidated.</summary>
        internal static void Invalidate(IEnumerable<IAssetName> names)
        {
            if (Textures.Count == 0)
            {
                return;
            }

            foreach (IAssetName name in names)
            {
                Textures.Remove(Normalize(name.BaseName));
            }
        }

        /// <summary>Asset names compare with either slash direction.</summary>
        private static string Normalize(string assetName) => assetName.Trim().Replace('\\', '/');
    }
}
