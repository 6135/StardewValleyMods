using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Sprites</c> asset, keyed <c>&lt;owner&gt;/&lt;name&gt;</c> and referenced as
    /// <c>sprite:&lt;owner&gt;/&lt;name&gt;</c>. A pack can reskin a UI by editing only this asset.
    /// </summary>
    internal sealed class SpriteDefinition
    {
        /// <summary>Texture asset name, e.g. Maps/springobjects or Mods/Your.Pack/Icons.</summary>
        public string? Texture { get; set; }

        /// <summary>Source rectangle "x,y,w,h" (empty = the whole texture).</summary>
        public string? Source { get; set; }

        /// <summary>3x3 tile region "x,y,w,h" used when the sprite is a box texture (the theme's 9-slice convention; empty = Source).</summary>
        public string? Border { get; set; }

        /// <summary>Default draw scale where the element does not set one.</summary>
        public string? Scale { get; set; }

        /// <summary>Default tint where the element does not set one.</summary>
        public string? Tint { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
