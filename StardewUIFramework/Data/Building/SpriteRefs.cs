using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Rendering;

namespace UIFramework.Data.Building
{
    /// <summary>A resolved image reference: the texture plus the optional source, box region, scale and tint it carries.</summary>
    internal readonly record struct SpriteRef(Texture2D? Texture, Rectangle? Source, Rectangle? BoxSource, float? Scale, Color? Tint);

    /// <summary>
    /// One-string image references, accepted everywhere data takes a texture:
    /// <list type="bullet">
    ///   <item><c>sprite:&lt;owner&gt;/&lt;name&gt;</c> (or <c>sprite:&lt;name&gt;</c> for the menu's owner): an entry of the <c>Sprites</c> asset, so a pack can reskin a UI without touching the tree;</item>
    ///   <item><c>item:(O)24</c>: an item's sprite;</item>
    ///   <item><c>asset:Maps/springobjects@0,0,16,16</c>: a texture asset with an optional source rectangle;</item>
    ///   <item>a bare asset name (<c>Mods/6135.UIFramework/TextBoxSmall</c>), also with an optional <c>@x,y,w,h</c>.</item>
    /// </list>
    /// </summary>
    internal sealed class SpriteRefs
    {
        internal const string SpritePrefix = "sprite:";
        internal const string ItemPrefix = "item:";
        internal const string AssetPrefix = "asset:";

        private Dictionary<string, SpriteDefinition> sprites = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Content hash of the sprites asset (a change rebuilds every data menu).</summary>
        internal string Hash { get; private set; } = string.Empty;

        /// <summary>Number of sprites defined.</summary>
        internal int Count => sprites.Count;

        /// <summary>The sprite keys, for <c>ui_data</c>.</summary>
        internal IEnumerable<string> Keys => sprites.Keys;

        /// <summary>Replace the sprite definitions.</summary>
        internal void Load(Dictionary<string, SpriteDefinition> definitions)
        {
            sprites = new Dictionary<string, SpriteDefinition>(definitions, StringComparer.OrdinalIgnoreCase);
            Hash = DefinitionHash.Of(new SortedDictionary<string, SpriteDefinition>(sprites, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>The <c>Sprites</c> key a <c>sprite:</c> reference names (the owner is prefixed to a bare name).</summary>
        internal static string SpriteKey(string name, string owner) => name.Contains('/') ? name : owner + "/" + name;

        /// <summary>Check the shape of a reference without loading anything (validator); <paramref name="error"/> explains a problem.</summary>
        internal bool Check(string reference, string owner, out string error)
        {
            error = string.Empty;
            string text = reference.Trim();
            if (text.StartsWith(SpritePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string key = SpriteKey(text.Substring(SpritePrefix.Length).Trim(), owner);
                if (!sprites.ContainsKey(key))
                {
                    error = $"sprite '{key}' is not defined in {DataAssets.Sprites}.";
                    return false;
                }

                return true;
            }

            if (text.StartsWith(ItemPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string id = text.Substring(ItemPrefix.Length).Trim();
                if (id.Length == 0 || ItemRegistry.GetData(id) == null)
                {
                    error = $"no item matches '{id}' (use a qualified id like (O)24).";
                    return false;
                }

                return true;
            }

            string asset = text.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) ? text.Substring(AssetPrefix.Length) : text;
            SplitSource(asset, out string name, out string? rect);
            if (name.Length == 0)
            {
                error = "the reference names no texture.";
                return false;
            }

            if (rect != null && ThemeData.ParseRectangle(rect) == null)
            {
                error = $"'{rect}' is not a rectangle 'x,y,width,height'.";
                return false;
            }

            return true;
        }

        /// <summary>Resolve a reference; false (with <paramref name="error"/>) when it names nothing usable.</summary>
        internal bool TryResolve(string reference, string owner, out SpriteRef sprite, out string error)
        {
            sprite = default;
            if (!Check(reference, owner, out error))
            {
                return false;
            }

            string text = reference.Trim();
            if (text.StartsWith(SpritePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string key = SpriteKey(text.Substring(SpritePrefix.Length).Trim(), owner);
                SpriteDefinition def = sprites[key];
                Texture2D? texture = TextureCache.Load(def.Texture, $"Sprite '{key}'");
                if (texture == null)
                {
                    error = $"sprite '{key}' names texture '{def.Texture}' which could not be loaded.";
                    return false;
                }

                sprite = new SpriteRef(
                    texture,
                    ThemeData.ParseRectangle(def.Source),
                    ThemeData.ParseRectangle(def.Border),
                    def.Scale != null && ValueParsers.TryParseDouble(def.Scale, out double scale) ? (float)scale : null,
                    def.Tint != null && ValueParsers.TryParseColor(def.Tint, out Color tint) ? tint : null);
                return true;
            }

            if (text.StartsWith(ItemPrefix, StringComparison.OrdinalIgnoreCase))
            {
                ParsedItemData data = ItemRegistry.GetDataOrErrorItem(text.Substring(ItemPrefix.Length).Trim());
                sprite = new SpriteRef(data.GetTexture(), data.GetSourceRect(0, null), null, null, null);
                return true;
            }

            string asset = text.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) ? text.Substring(AssetPrefix.Length) : text;
            SplitSource(asset, out string name, out string? rect);
            Texture2D? loaded = TextureCache.Load(name, $"Image reference '{reference}'");
            if (loaded == null)
            {
                error = $"texture '{name}' could not be loaded.";
                return false;
            }

            sprite = new SpriteRef(loaded, rect == null ? null : ThemeData.ParseRectangle(rect), null, null, null);
            return true;
        }

        /// <summary>Split <c>Name@x,y,w,h</c>.</summary>
        private static void SplitSource(string text, out string name, out string? rect)
        {
            int at = text.LastIndexOf('@');
            if (at < 0)
            {
                name = text.Trim();
                rect = null;
                return;
            }

            name = text.Substring(0, at).Trim();
            rect = text.Substring(at + 1).Trim();
        }
    }
}
