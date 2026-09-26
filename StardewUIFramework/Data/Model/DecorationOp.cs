using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// One decoration of a contribution (v1.7): an edit of another mod's menu tree, applied through the contributor's
    /// API instance every time the menu opens (the previous application is undone first, so the edits never pile up).
    /// Elements inside a sealed subtree refuse every edit (a logged error).
    /// </summary>
    internal sealed class DecorationOp
    {
        /// <summary>Hide, Show, Set, InsertBefore, InsertAfter, Append, Move, Remove or Replace.</summary>
        public string? Op { get; set; }

        /// <summary>The element id the edit applies to (for Append: the container the children are added to).</summary>
        public string? Target { get; set; }

        /// <summary>InsertBefore / InsertAfter / Append / Replace: the elements to add (built by the contributor).</summary>
        [JsonConverter(typeof(ElementListConverter))]
        public List<ElementDefinition>? Children { get; set; }

        /// <summary>Set: the members to change and their values ({ "Text": "${ctx.name}", "Enabled": false }); Visible, Enabled, Text, Tooltip, TooltipTitle, Tag, Width, Height, Margin, HorizontalAlign, VerticalAlign, Color, Font.</summary>
        public Dictionary<string, string>? Fields { get; set; }

        /// <summary>Move: the element Target is moved in front of.</summary>
        public string? Before { get; set; }

        /// <summary>Move: the element Target is moved behind.</summary>
        public string? After { get; set; }

        /// <summary>Move: the container Target is moved to the end of.</summary>
        public string? Into { get; set; }

        /// <summary>Fields that match no member (typos).</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken>? Unknown { get; set; }
    }

    /// <summary>The decoration operations and the members Set accepts.</summary>
    internal static class DecorationOps
    {
        internal const string Hide = "Hide";
        internal const string Show = "Show";
        internal const string Set = "Set";
        internal const string InsertBefore = "InsertBefore";
        internal const string InsertAfter = "InsertAfter";
        internal const string Append = "Append";
        internal const string Move = "Move";
        internal const string Remove = "Remove";
        internal const string Replace = "Replace";

        /// <summary>Every operation name.</summary>
        internal static readonly string[] All = { Hide, Show, Set, InsertBefore, InsertAfter, Append, Move, Remove, Replace };

        /// <summary>The members <see cref="Set"/> can change.</summary>
        internal static readonly string[] SetFields =
        {
            "Visible", "Enabled", "Text", "Tooltip", "TooltipTitle", "Tag", "Width", "Height", "Margin", "HorizontalAlign", "VerticalAlign", "Color", "Font"
        };

        /// <summary>The canonical spelling of an operation, or null.</summary>
        internal static string? Canonical(string? op)
        {
            string text = op?.Trim() ?? string.Empty;
            return All.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True for the operations that add Children.</summary>
        internal static bool AddsChildren(string op) => op is InsertBefore or InsertAfter or Append or Replace;
    }
}
