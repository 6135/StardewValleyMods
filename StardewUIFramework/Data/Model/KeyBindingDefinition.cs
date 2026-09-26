using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One key handler of an element or menu (<c>Keys</c>), the data form of <c>OnKey</c>: when <see cref="Key"/> is
    /// pressed with the given modifiers, <see cref="Actions"/> run and the key counts as handled.
    /// </summary>
    internal sealed class KeyBindingDefinition
    {
        /// <summary>The key (an XNA key name such as Enter, Delete, F5, A, Up).</summary>
        public string? Key { get; set; }

        /// <summary>Shift must be held (true) or not held (false); unset = either.</summary>
        public string? Shift { get; set; }

        /// <summary>Control must be held (true) or not held (false); unset = either.</summary>
        public string? Ctrl { get; set; }

        /// <summary>Alt must be held (true) or not held (false); unset = either.</summary>
        public string? Alt { get; set; }

        /// <summary>Actions run when the key matches (event.key, event.shift, event.ctrl, event.alt).</summary>
        [JsonConverter(typeof(ActionListConverter))]
        public List<ActionDefinition>? Actions { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }
}
