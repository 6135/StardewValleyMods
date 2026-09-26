using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// A template (v1.7): a parameterized element tree expanded where it is used. Menu-local templates live in a menu's
    /// <c>Templates</c>, owner-wide ones in the owner's <c>Owners</c> entry. An instance is written
    /// <c>{ "Type": "&lt;template&gt;", "&lt;param&gt;": value, "Children": [...] }</c> (or <c>"Template": "&lt;template&gt;"</c>);
    /// its extra fields are the arguments (<c>args.&lt;param&gt;</c> in the body) and its children go into the body's
    /// <c>Outlet</c> placeholders.
    /// </summary>
    internal class TemplateDefinition
    {
        /// <summary>The parameters: { "label": { "Type": "string", "Default": "", "Required": true } }. Arguments are validated against them when the data loads; missing ones take their Default.</summary>
        public Dictionary<string, ParamDefinition>? Params { get; set; }

        /// <summary>The body: the elements an instance expands to (ids are prefixed with the instance's id). Outlet placeholders ({ "Outlet": "header" }, or { "Type": "Outlet" } for the default one) receive the instance's children.</summary>
        public List<ElementDefinition>? Children { get; set; }

        /// <summary>Lay the body out in a row instead of a column.</summary>
        public string? Horizontal { get; set; }

        /// <summary>Pixels between the body's elements (default 0 for templates; composites are always a column).</summary>
        public string? Spacing { get; set; }

        /// <summary>Default cross-axis alignment of the body's elements.</summary>
        public string? Alignment { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>One parameter of a template or data composite.</summary>
    internal sealed class ParamDefinition
    {
        /// <summary>The value type: string (default), number, bool or any. Literal arguments are checked against it; values are converted to it when read.</summary>
        public string? Type { get; set; }

        /// <summary>The value used when an instance does not set the argument.</summary>
        public JToken? Default { get; set; }

        /// <summary>An instance must set the argument (a validation error otherwise).</summary>
        public string? Required { get; set; }

        /// <summary>What the parameter is for (documentation only).</summary>
        public string? Description { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>
    /// One entry of the <c>Mods/6135.UIFramework/Composites</c> asset (v1.7), keyed by the composite's global name
    /// (<c>&lt;ModId&gt;.&lt;Name&gt;</c>). A data composite is registered like <c>DefineComposite</c>, so C# code can
    /// instantiate it with <c>AddComposite</c> and data with <c>"Type": "&lt;name&gt;"</c>; the owner is the longest loaded
    /// mod id the name starts with, or <see cref="Owner"/>. Its body reads <c>args.*</c>; it can expose values
    /// (<c>el[id].&lt;key&gt;</c>, <c>IUIComposite.GetValue</c>) and commands (<c>_Invoke #id.cmd</c>), and raise events
    /// with <c>_Publish</c> (<c>"On": { "event": [...] }</c> on the instance, <c>Subscribe</c> in C#). A changed
    /// definition rebuilds every live instance.
    /// </summary>
    internal sealed class DataCompositeDefinition : TemplateDefinition
    {
        /// <summary>The owning mod or content pack (default: the longest loaded mod id the composite's name starts with).</summary>
        public string? Owner { get; set; }

        /// <summary>Values the composite exposes: { "value": "args.value", "label": "upper(args.label)" } (expressions over the body's scope).</summary>
        public Dictionary<string, string>? Expose { get; set; }

        /// <summary>Commands the composite exposes: { "reset": "6135.UIFramework_SetState ..." } (run in the body's scope; _Invoke #id.reset, IUIComposite.Invoke).</summary>
        [JsonProperty(ItemConverterType = typeof(ActionListConverter))]
        public Dictionary<string, List<ActionDefinition>>? Commands { get; set; }

        /// <summary>The events the body raises with 6135.UIFramework_Publish; when set, an instance's <c>On</c> names are checked against it.</summary>
        [JsonConverter(typeof(StringListConverter))]
        public List<string>? Publish { get; set; }
    }
}
