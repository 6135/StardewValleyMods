using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace UIFramework.Core.Export
{
    internal sealed partial class JsonEmitter
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The data field of a column member (<c>ColumnDefinition</c>): the cell builder is <c>Cell</c>, the cell tooltip <c>Tooltip</c>.</summary>
        private static string ColumnKey(string name)
        {
            return name switch
            {
                "BuildCell" => "Cell",
                "CellTooltip" => "Tooltip",
                _ => name
            };
        }

        /// <summary>The data field for a C# member (null when there is none).</summary>
        private static string? DataKey(string kind, string name)
        {
            return name switch
            {
                "Texture" when kind == TreeKinds.Image => "Sprite",
                "SortColumn" => "Sort",
                "ItemCount" or "RowCount" or "VetoedContributors" or "VisiblePredicate" => null,
                _ => name
            };
        }

        /// <summary>Add one non-default property (as a member, or a TODO comment when it cannot be serialized).</summary>
        private void AddProperty(JObj o, TreeProperty p, string? key, int rank)
        {
            if (p.IsDefault)
            {
                return;
            }

            if (key == null)
            {
                o.Comment(NoFieldNote(p), rank);
                return;
            }

            switch (p.Origin)
            {
                case TreeValueOrigin.Opaque:
                    if (p.Value != null)
                    {
                        o.Add(key, Value(p), rank, p.Note == null ? null : "TODO: " + p.Note);
                    }
                    else
                    {
                        o.Comment(OpaqueNote(key), rank);
                    }
                    return;
                case TreeValueOrigin.Delegate:
                    if (p.Value == null)
                    {
                        o.Comment($"TODO: {key} was a C# delegate with no current value", rank);
                    }
                    else
                    {
                        o.Add(key, Value(p), rank, key == "Value" ? GetterNote : DelegateNote);
                    }
                    return;
            }

            o.Add(key, Value(p), rank);
        }

        private static string NoFieldNote(TreeProperty p)
        {
            return p.Name switch
            {
                "ItemCount" or "RowCount" => $"TODO: {p.Name} was a C# delegate ({p.Value} now); give the element a Source",
                _ => $"TODO: {p.Name} has no data field"
            };
        }

        private static string OpaqueNote(string key)
        {
            if (ActionEvents.Contains(key))
            {
                return $"TODO: {key} was a C# delegate; write it as an action list, e.g. \"{key}\": [ \"...\" ]";
            }

            return key switch
            {
                "OnKey" => "TODO: OnKey was a C# delegate; use \"Keys\" entries",
                "OnUpdate" => "TODO: OnUpdate was a C# delegate; use \"OnUpdate\" actions",
                "Validate" => "TODO: Validate was a C# delegate; use a Validate expression",
                "Texture" or "Sprite" or "BoxTexture" => $"TODO: {key} was a texture; use a sprite:, asset: or item: reference",
                "Tag" => "TODO: Tag was a C# object; only string tags have a data form",
                "OnDrawExtra" => "TODO: OnDrawExtra was a C# delegate; register it with RegisterDrawHook and set \"DrawExtra\": \"<ModId>/<name>\"",
                "OnDrawOverlay" => "TODO: OnDrawOverlay was a C# delegate; register it with RegisterDrawHook and set \"DrawOverlay\": \"<ModId>/<name>\"",
                _ => $"TODO: {key} was a C# delegate; it has no data form"
            };
        }

        private JToken Value(TreeProperty p)
        {
            bool text = p.Origin == TreeValueOrigin.Delegate && p.Kind == TreeValueKind.String;
            return p.Kind switch
            {
                TreeValueKind.Style => StyleObject((TreeStyle)p.Value!),
                TreeValueKind.Tooltip => TooltipObject((TreeTooltip)p.Value!),
                TreeValueKind.Icon => IconValue((TreeIcon)p.Value!),
                TreeValueKind.StringList => StringList((string[])p.Value!),
                _ => Scalar(p.Kind, p.Value, text)
            };
        }

        private JObj StyleObject(TreeStyle style)
        {
            var o = new JObj();
            foreach (TreeProperty p in style.Properties)
            {
                AddProperty(o, p, p.Name, 0);
            }

            return o;
        }

        private static JToken IconValue(TreeIcon icon)
        {
            if (icon.TextureName == null)
            {
                return new JRaw(JsonConvert.ToString("TODO"));
            }

            string source = icon.Source.HasValue ? "@" + RectText(icon.Source.Value) : string.Empty;
            return new JRaw(JsonConvert.ToString($"asset:{icon.TextureName}{source}"));
        }

        private static JObj TooltipObject(TreeTooltip tooltip)
        {
            var o = new JObj();
            if (tooltip.MaxWidth > 0)
            {
                o.Add("MaxWidth", Scalar((long)tooltip.MaxWidth), 0);
            }

            var blocks = new JArr();
            foreach (TreeTooltipBlock b in tooltip.Blocks)
            {
                blocks.Add(TooltipBlock(b));
            }

            o.Add("Blocks", blocks, 0);
            return o;
        }

        private static JObj TooltipBlock(TreeTooltipBlock b)
        {
            var o = new JObj();
            switch (b.Kind)
            {
                case TooltipBlockKind.Title:
                case TooltipBlockKind.Line:
                    o.Add("Type", Scalar(b.Kind.ToString()), 0);
                    o.Add("Text", Scalar(EscapeText(b.Text ?? string.Empty)), 0, DelegateNote);
                    if (b.Color.HasValue)
                    {
                        o.Add("Color", Scalar(ColorText(b.Color.Value)), 0);
                    }
                    break;
                case TooltipBlockKind.Icon:
                    o.Add("Type", Scalar("Icon"), 0);
                    if (b.TextureName != null)
                    {
                        o.Add("Sprite", Scalar(b.TextureName), 0, "TODO: guessed from the texture's asset name");
                    }
                    else
                    {
                        o.Comment("TODO: the icon was a texture; use a sprite:, asset: or item: reference", 0);
                    }

                    if (b.Source.HasValue)
                    {
                        o.Add("Source", Scalar(RectText(b.Source.Value)), 0);
                    }

                    if (!Numbers.Same(b.Scale, 1f))
                    {
                        o.Add("Scale", Scalar(TreeValueKind.Float, b.Scale, false), 0);
                    }
                    break;
                case TooltipBlockKind.Item:
                    o.Add("Type", Scalar("Item"), 0);
                    o.Add("Item", Scalar(b.ItemId), 0);
                    break;
                case TooltipBlockKind.ItemInstance:
                    o.Add("Type", Scalar("Item"), 0);
                    if (b.ItemPreview != null)
                    {
                        o.Add("Item", Scalar(b.ItemPreview), 0, "TODO: was an item instance getter; this is its current item");
                    }
                    else
                    {
                        o.Comment("TODO: was an item instance getter with no current item", 0);
                    }
                    break;
                case TooltipBlockKind.Divider:
                    o.Add("Type", Scalar("Divider"), 0);
                    break;
                case TooltipBlockKind.Money:
                    o.Add("Type", Scalar("Money"), 0);
                    o.Add("Amount", Scalar((long)b.Amount), 0, DelegateNote);
                    break;
            }

            if (b.HasWhen)
            {
                o.Comment("TODO: the block had a visibility condition; add a \"When\" expression", 0);
            }

            if (b.HasColorFunc)
            {
                o.Comment("TODO: the block had a dynamic color; use an expression in \"Color\"", 0);
            }

            return o;
        }

        /// <summary>Sort rank of a member (stable within a rank, so type members keep the reader's order).</summary>
        private static int Rank(string? key)
        {
            if (key == null)
            {
                return 20;
            }

            if (key is "Type" or "Composite")
            {
                return 0;
            }

            if (key == "Id")
            {
                return 1;
            }

            if (MainMembers.Contains(key))
            {
                return 10;
            }

            if (LayoutMembers.Contains(key))
            {
                return 50;
            }

            if (StateMembers.Contains(key))
            {
                return 60;
            }

            if (TooltipMembers.Contains(key))
            {
                return 70;
            }

            return key switch
            {
                "Style" => 80,
                "Args" => 95,
                "Children" => 100,
                _ when key.StartsWith("On", StringComparison.Ordinal) => 90,
                _ => 20
            };
        }
    }
}
