using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>Inline style of a data element (the data form of <c>IUIStyle</c>). Every member is optional.</summary>
    internal sealed class StyleDefinition
    {
        /// <summary>Font: small, dialogue or tiny.</summary>
        public string? Font { get; set; }

        /// <summary>Text color.</summary>
        public string? TextColor { get; set; }

        /// <summary>Tint applied while hovered or focused.</summary>
        public string? HoverColor { get; set; }

        /// <summary>Box texture: an image reference (sprite:Owner/name, asset:Path@x,y,w,h) or a texture asset name.</summary>
        public string? BoxTexture { get; set; }

        /// <summary>3x3 tile region "x,y,w,h" of the box texture (overrides the sprite's Border / Source).</summary>
        public string? BoxSource { get; set; }

        /// <summary>Box scale.</summary>
        public string? BoxScale { get; set; }

        /// <summary>Padding inside boxes.</summary>
        public string? Padding { get; set; }

        /// <summary>Draw text shadows.</summary>
        public string? TextShadow { get; set; }

        /// <summary>Click sound cue (empty = silent).</summary>
        public string? ClickSound { get; set; }

        /// <summary>Hover sound cue (empty = silent).</summary>
        public string? HoverSound { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
