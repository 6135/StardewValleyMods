using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace UIFramework.Core.Export
{
    /// <summary>
    /// Writes a <see cref="TreeMenu"/> as one entry of the <c>Mods/6135.UIFramework/Menus</c> data asset (the shape of
    /// <c>Data/Model/MenuDefinition</c> / <c>ElementDefinition</c>): menu options first, then the root layout, the
    /// default / cancel buttons, events and <c>Children</c>. Defaults are omitted; keys are ordered type / id, main
    /// value, type members, layout, state, tooltips, style, events, children. Delegates are written as their current
    /// value, and what cannot be serialized (event delegates, textures, custom components) becomes a <c>// TODO</c>
    /// comment (SMAPI and Content Patcher read JSON with comments).
    /// <para>
    /// <c>List</c>, <c>DataGrid</c> (<c>Columns</c> of <c>ColumnDefinition</c>, <c>Sort</c>), <c>Form</c> and rich tooltip
    /// blocks use the v1.5 model's field names; composites use the v1.6 <c>Type: "Composite"</c> element with <c>Composite</c> and
    /// <c>Args</c>, and draw callbacks point at <c>DrawExtra</c> / <c>DrawOverlay</c> hooks.
    /// </para>
    /// </summary>
    internal sealed partial class JsonEmitter
    {
        private const string DelegateNote = "TODO: was a C# delegate; this is its current value";
        private const string GetterNote = "TODO: was a C# getter / setter; this is its current value (bind it to state instead)";

        /// <summary>Delegates that have a data form as an action list.</summary>
        private static readonly HashSet<string> ActionEvents = new(StringComparer.Ordinal)
        {
            "OnClick", "OnRightClick", "OnHover", "OnHoverEnd", "OnFocus", "OnBlur", "OnValueChanged", "OnSubmit", "OnLink",
            "OnScroll", "OnOpen", "OnClose"
        };

        /// <summary>Main-value members (right after the type and id).</summary>
        private static readonly HashSet<string> MainMembers = new(StringComparer.Ordinal)
        {
            "Text", "Label", "Value", "Sprite", "Item", "Quality", "Count", "Choices", "Labels"
        };

        private static readonly HashSet<string> LayoutMembers = new(StringComparer.Ordinal)
        {
            "Margin", "Width", "Height", "HorizontalAlign", "VerticalAlign", "X", "Y", "Row", "Column", "RowSpan", "ColumnSpan"
        };

        private static readonly HashSet<string> StateMembers = new(StringComparer.Ordinal) { "Visible", "Enabled", "Sealed" };

        private static readonly HashSet<string> TooltipMembers = new(StringComparer.Ordinal)
        {
            "Tooltip", "TooltipTitle", "RichTooltip", "AccessibleName", "Tag"
        };

        private readonly StringBuilder sb = new();
        private readonly TreeMenu menu;

        private JsonEmitter(TreeMenu menu)
        {
            this.menu = menu;
        }

        /// <summary>The <c>Menus</c> entry for <paramref name="menu"/> (JSON with comments).</summary>
        internal static string Emit(TreeMenu menu) => new JsonEmitter(menu).Run();

        private string Run()
        {
            sb.Append($"// Exported by the UI Framework inspector: menu '{menu.Id}' of {menu.OwnerModId}.\n");
            sb.Append($"// Use it as the value of the \"{menu.OwnerModId}/{menu.Id}\" entry of Mods/6135.UIFramework/Menus, or as a standalone \"From\" file.\n");
            Write(MenuObject(), 0);
            sb.Append('\n');
            return sb.ToString();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Menu
        // ---------------------------------------------------------------------------------------------------------

        private JObj MenuObject()
        {
            var o = new JObj();
            foreach (TreeProperty p in menu.Options)
            {
                AddProperty(o, p, p.Name, 0);
            }

            foreach (TreeProperty p in menu.Root.Properties)
            {
                if (p.Name is "Horizontal" or "Spacing" or "Alignment")
                {
                    AddProperty(o, p, p.Name, 0);
                }
                else if (!p.IsDefault)
                {
                    o.Comment($"TODO: the root stack's {p.Name} has no data field; wrap the children in a Stack to keep it", 0);
                }
            }

            if (menu.DefaultButton != null)
            {
                o.Add("DefaultButton", Scalar(menu.DefaultButton.Id), 0);
            }

            if (menu.CancelButton != null)
            {
                o.Add("CancelButton", Scalar(menu.CancelButton.Id), 0);
            }

            foreach (TreeProperty p in menu.Events)
            {
                AddProperty(o, p, p.Name, 0);
            }

            AddChildren(o, menu.Root);
            return o;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Elements
        // ---------------------------------------------------------------------------------------------------------

        private void AddChildren(JObj o, TreeNode parent)
        {
            if (parent.ExcludedContributors.Count > 0)
            {
                o.Comment($"NOTE: {parent.ExcludedContributors.Count} contribution(s) from other mods ({string.Join(", ", parent.ExcludedContributors)}) are not exported; they are rebuilt by their ContributeTo when the menu opens", Rank("Children"));
            }

            if (parent.ChildrenNote != null)
            {
                o.Comment($"NOTE: the children are {parent.ChildrenNote}; they are not exported", Rank("Children"));
                return;
            }

            if (parent.Children.Count == 0)
            {
                return;
            }

            var children = new JArr();
            foreach (TreeNode child in parent.Children)
            {
                AddElement(children, child);
            }

            o.Add("Children", children, Rank("Children"));
        }

        private void AddElement(JArr list, TreeNode n)
        {
            switch (n.Kind)
            {
                case TreeKinds.Unknown:
                    list.Comment($"TODO: element '{n.Id}' is a {n.ClrType}, which this exporter does not know; its subtree was skipped");
                    return;
                case TreeKinds.Custom:
                    list.Comment($"TODO: element '{n.Id}' is a custom C# component (AddCustom); define a C# composite that adds it (DefineComposite) and use it here as {{ \"Type\": \"Composite\", \"Composite\": \"<ModId>.<Name>\" }} or as a custom tag {{ \"Type\": \"<ModId>.<Name>\" }}");
                    return;
            }

            var o = new JObj();
            switch (n.Kind)
            {
                case TreeKinds.Composite:
                    o.Add("Type", Scalar("Composite"), Rank("Type"));
                    o.Add("Composite", Scalar(n.CompositeName ?? string.Empty), Rank("Type"));
                    if (n.IsDataComposite)
                    {
                        o.Comment("NOTE: a data composite (Composites asset); its body is not exported, and children the instance placed in its Outlets must be written again as \"Children\" (with \"Outlet\": \"<name>\")", Rank("Type"));
                    }

                    break;
                case TreeKinds.CustomHost:
                    o.Add("Type", Scalar(TreeKinds.Stack), Rank("Type"));
                    o.Comment("TODO: was a custom C# component with a host (AddCustom + build); define a C# composite that adds it and use it here (\"Type\": \"Composite\"); its Children then go into the component's host (\"ContentTarget\")", Rank("Type"));
                    o.Add("Spacing", Scalar(0L), Rank("Spacing"));
                    break;
                default:
                    o.Add("Type", Scalar(n.Kind), Rank("Type"));
                    break;
            }

            o.Add("Id", Scalar(n.Id), Rank("Id"));
            foreach (TreeProperty p in n.Properties)
            {
                AddProperty(o, p, DataKey(n.Kind, p.Name), Rank(DataKey(n.Kind, p.Name)));
            }

            if (n.Kind == TreeKinds.List)
            {
                o.Comment("TODO: the rows were built by a C# delegate; give the list a \"Source\" and write one row as its \"RowTemplate\"", Rank("Value"));
            }

            if (n.Kind == TreeKinds.Form)
            {
                o.Comment($"TODO: the fields were generated from a C# model ({n.ModelType}); expose it (ExposeModel) and set \"Model\", or list \"Fields\" bound to state", Rank("Value"));
            }

            AddArgs(o, n);
            AddColumns(o, n);
            AddChildren(o, n);
            list.Add(o.Sorted());
        }

        private void AddArgs(JObj o, TreeNode n)
        {
            if (n.Args.Count == 0)
            {
                return;
            }

            var args = new JObj();
            foreach (TreeProperty a in n.Args)
            {
                if (a.Origin == TreeValueOrigin.Opaque)
                {
                    args.Comment($"TODO: argument '{a.Name}' was set with {a.Note} (a C# delegate or object)", 0);
                }
                else
                {
                    args.Add(a.Name, Value(a), 0, a.Origin == TreeValueOrigin.Delegate ? DelegateNote : null);
                }
            }

            o.Add("Args", args, Rank("Args"));
        }

        private void AddColumns(JObj o, TreeNode n)
        {
            if (n.Kind != TreeKinds.DataGrid || n.Columns.Count == 0)
            {
                return;
            }

            var columns = new JArr();
            foreach (TreeColumn c in n.Columns)
            {
                var co = new JObj();
                co.Add("Id", Scalar(c.Id), 0);
                co.Add("Header", Scalar(EscapeText(c.Header)), 0, DelegateNote);
                foreach (TreeProperty p in c.Properties)
                {
                    AddProperty(co, p, ColumnKey(p.Name), 0);
                }

                columns.Add(co);
            }

            o.Add("Columns", columns, Rank("Args"));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Scalars
        // ---------------------------------------------------------------------------------------------------------

        private static JRaw Scalar(string value) => new(JsonConvert.ToString(value));

        private static JRaw Scalar(long value) => new(value.ToString(CultureInfo.InvariantCulture));

        private static JToken Scalar(TreeValueKind kind, object? value, bool text)
        {
            return kind switch
            {
                TreeValueKind.Bool => new JRaw((bool)value! ? "true" : "false"),
                TreeValueKind.Int => new JRaw(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"),
                TreeValueKind.Float => Number((float)value!),
                TreeValueKind.Double => Number((double)value!),
                TreeValueKind.String => Scalar(text ? EscapeText(value as string ?? string.Empty) : value as string ?? string.Empty),
                TreeValueKind.Enum => Scalar(value!.ToString()!),
                TreeValueKind.Color => Scalar(ColorText((Color)value!)),
                TreeValueKind.Rect => Scalar(RectText((Rectangle)value!)),
                TreeValueKind.Margin => Scalar(MarginText((int[])value!)),
                _ => Scalar(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            };
        }

        private static JRaw Number(double value)
        {
            return double.IsFinite(value)
                ? new JRaw(value.ToString("R", CultureInfo.InvariantCulture))
                : Scalar(value.ToString(CultureInfo.InvariantCulture));
        }

        private static JRaw Number(float value)
        {
            return float.IsFinite(value)
                ? new JRaw(value.ToString("0.######", CultureInfo.InvariantCulture))
                : Scalar(value.ToString(CultureInfo.InvariantCulture));
        }

        private static JArr StringList(string[] values)
        {
            var list = new JArr();
            foreach (string value in values)
            {
                list.Add(Scalar(value));
            }

            return list;
        }

        /// <summary>Text fields interpolate <c>${…}</c>, so a literal <c>${</c> is written <c>$${</c>.</summary>
        private static string EscapeText(string value) => value.Replace("${", "$${", StringComparison.Ordinal);

        private static string ColorText(Color c)
        {
            string rgb = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            return c.A == 255 ? rgb : rgb + c.A.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string RectText(Rectangle r) => string.Join(",", new[] { r.X, r.Y, r.Width, r.Height }.Select(i => i.ToString(CultureInfo.InvariantCulture)));

        private static string MarginText(int[] m)
        {
            int l = m[0], t = m[1], r = m[2], b = m[3];
            int[] parts = l == t && t == r && r == b ? new[] { l } : l == r && t == b ? new[] { l, t } : m;
            return string.Join(",", parts.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
