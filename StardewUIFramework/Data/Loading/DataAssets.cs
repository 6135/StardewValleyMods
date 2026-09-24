using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using UIFramework.Data.Model;

namespace UIFramework.Data.Loading
{
    /// <summary>
    /// The data assets Content Patcher packs edit (<c>EditData</c>) and the textures the framework serves for them.
    /// Each asset is provided with the entries C# mods imported (<c>ImportData</c>, <see cref="Bridge.DataImport"/>; otherwise
    /// empty) as its base layer (Exclusive, like the theme asset), so packs only add or patch entries.
    /// </summary>
    internal static class DataAssets
    {
        /// <summary>Data menus: <c>Dictionary&lt;string, MenuDefinition&gt;</c> keyed <c>&lt;owner&gt;/&lt;menuId&gt;</c>.</summary>
        internal const string Menus = "Mods/6135.UIFramework/Menus";

        /// <summary>Named sprites: <c>Dictionary&lt;string, SpriteDefinition&gt;</c> keyed <c>&lt;owner&gt;/&lt;name&gt;</c>.</summary>
        internal const string Sprites = "Mods/6135.UIFramework/Sprites";

        /// <summary>Data HUD widgets: <c>Dictionary&lt;string, HudDefinition&gt;</c> keyed <c>&lt;owner&gt;/&lt;hudId&gt;</c>.</summary>
        internal const string Huds = "Mods/6135.UIFramework/Huds";

        /// <summary>Per-owner settings: <c>Dictionary&lt;string, OwnerDefinition&gt;</c> keyed by mod id.</summary>
        internal const string Owners = "Mods/6135.UIFramework/Owners";

        /// <summary>Data composites (v1.7): <c>Dictionary&lt;string, DataCompositeDefinition&gt;</c> keyed by global name (<c>&lt;ModId&gt;.&lt;Name&gt;</c>).</summary>
        internal const string Composites = "Mods/6135.UIFramework/Composites";

        /// <summary>Contributions to other mods' menus (v1.7): <c>Dictionary&lt;string, ContributionDefinition&gt;</c> keyed <c>&lt;contributor&gt;/&lt;name&gt;</c>.</summary>
        internal const string Contributions = "Mods/6135.UIFramework/Contributions";

        /// <summary>The bundled small text box texture (<c>assets/text_box_small.png</c>), so data can reference it.</summary>
        internal const string TextBoxSmall = "Mods/6135.UIFramework/TextBoxSmall";

        /// <summary>Short name of an asset for messages (<c>Menus</c>).</summary>
        internal static string ShortName(string asset) => asset.Substring(asset.LastIndexOf('/') + 1);

        /// <summary>Provide the framework's assets.</summary>
        internal static void OnAssetRequested(AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(Menus))
            {
                e.LoadFrom(Bridge.DataImport.Menus, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(Sprites))
            {
                e.LoadFrom(Bridge.DataImport.Sprites, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(Huds))
            {
                e.LoadFrom(Bridge.DataImport.Huds, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(Owners))
            {
                e.LoadFrom(Bridge.DataImport.Owners, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(Composites))
            {
                e.LoadFrom(Bridge.DataImport.Composites, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(Contributions))
            {
                e.LoadFrom(Bridge.DataImport.Contributions, AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo(TextBoxSmall))
            {
                e.LoadFromModFile<Texture2D>("assets/text_box_small.png", AssetLoadPriority.Exclusive);
            }
            else
            {
                // not one of ours
            }
        }
    }
}
