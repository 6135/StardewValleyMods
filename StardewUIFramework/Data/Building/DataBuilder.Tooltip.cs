using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// Rich tooltips as data (<c>RichTooltip</c> on any element, <c>RowTooltip</c> on a DataGrid). A definition is
    /// compiled once (values, image references, messages) into a <see cref="CompiledTooltip"/>, then instantiated per
    /// scope (per row for grids) as a <see cref="RichTooltip"/> whose text, amount, item, color and <c>When</c>
    /// delegates evaluate in that scope each time the tooltip draws.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>Compile a tooltip definition; null when it has no usable block (reported).</summary>
        private CompiledTooltip? CompileTooltip(TooltipDefinition def, string owner, DataPath path, PropertyApplier a)
        {
            var blocks = new List<CompiledBlock>();
            string? maxWidthText = def.MaxWidth;

            // a named tooltip of the owner comes first (its blocks keep their own path for messages)
            if (!string.IsNullOrWhiteSpace(def.From))
            {
                string name = def.From.Trim();
                if (owners(owner)?.Tooltips is { } named && named.TryGetValue(name, out TooltipDefinition? shared) && shared != null)
                {
                    CompileBlocks(shared.Blocks, owner, DataPath.Entry(DataAssets.ShortName(DataAssets.Owners), owner).Field("Tooltips").Field(name), a, blocks);
                    maxWidthText ??= shared.MaxWidth;
                }
                else
                {
                    a.Log.Error(path.Field("From"), $"'{owner}' has no tooltip '{name}' in {DataAssets.Owners} (Tooltips).");
                }
            }

            CompileBlocks(def.Blocks, owner, path, a, blocks);
            if (blocks.Count == 0)
            {
                a.Log.Warn(path, "the rich tooltip has no blocks; it is ignored.");
                return null;
            }

            ValueSource<int>? maxWidth = a.Source(maxWidthText, ValueParsers.Int, path.Field("MaxWidth"));
            return new CompiledTooltip(blocks, maxWidth);
        }

        private void CompileBlocks(List<TooltipBlockDefinition>? definitions, string owner, DataPath path, PropertyApplier a, List<CompiledBlock> blocks)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                TooltipBlockDefinition? block = definitions[i];
                if (block == null)
                {
                    continue;
                }

                CompiledBlock? compiled = CompileBlock(block, owner, path.Field("Blocks").Index(i, null), a);
                if (compiled != null)
                {
                    blocks.Add(compiled);
                }
            }
        }

        private CompiledBlock? CompileBlock(TooltipBlockDefinition def, string owner, DataPath path, PropertyApplier a)
        {
            string? kind = TooltipBlockKinds.Canonical(def.Type ?? (def.Text != null ? "Line" : null));
            if (kind == null)
            {
                string? suggestion = def.Type != null ? DataValidator.Suggest(def.Type, TooltipBlockKinds.All) : null;
                a.Log.Error(path.Field("Type"), $"'{def.Type}' is not a block type (Title, Line, Icon, Item, Divider, Money){(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; the block is skipped.");
                return null;
            }

            var block = new CompiledBlock(kind)
            {
                When = a.Source(def.When, ValueParsers.Bool, path.Field("When"))
            };
            switch (kind)
            {
                case "Title":
                case "Line":
                    block.Text = a.Source(def.Text ?? string.Empty, ValueParsers.Text, path.Field("Text"));
                    block.Color = a.Source(def.Color, ValueParsers.ColorValue, path.Field("Color"));
                    break;
                case "Icon":
                {
                    if (string.IsNullOrWhiteSpace(def.Sprite))
                    {
                        a.Log.Error(path.Field("Sprite"), "an Icon block needs a Sprite (sprite:, item: or asset: reference); it is skipped.");
                        return null;
                    }

                    if (!sprites.TryResolve(def.Sprite, owner, out SpriteRef sprite, out string error))
                    {
                        a.Log.Error(path.Field("Sprite"), error);
                        return null;
                    }

                    block.Sprite = sprite;
                    if (def.Source != null && ValueParsers.RectangleValue.Parse(def.Source, out Rectangle source))
                    {
                        block.Sprite = sprite with { Source = source };
                    }

                    block.Scale = def.Scale != null && ValueParsers.TryParseDouble(def.Scale, out double scale) ? (float)scale : sprite.Scale ?? 1f;
                    break;
                }

                case "Item":
                    if (string.IsNullOrWhiteSpace(def.Item))
                    {
                        a.Log.Error(path.Field("Item"), "an Item block needs an Item (a qualified id, an item query or ${row.item}); it is skipped.");
                        return null;
                    }

                    block.Item = a.Source(def.Item.Trim(), ValueParsers.Raw, path.Field("Item"));
                    break;
                case "Money":
                    block.Amount = a.Source(def.Amount ?? "0", ValueParsers.Int, path.Field("Amount"));
                    break;
            }

            return block;
        }

        /// <summary>One compiled tooltip block (only the members of its kind are set).</summary>
        private sealed class CompiledBlock
        {
            internal CompiledBlock(string kind)
            {
                Kind = kind;
            }

            internal string Kind { get; }
            internal ValueSource<bool>? When { get; init; }
            internal ValueSource<string>? Text { get; set; }
            internal ValueSource<Color>? Color { get; set; }
            internal SpriteRef Sprite { get; set; }
            internal float Scale { get; set; } = 1f;
            internal ValueSource<DataValue>? Item { get; set; }
            internal ValueSource<int>? Amount { get; set; }
        }

        /// <summary>A compiled rich tooltip; <see cref="Create"/> instantiates it for a scope.</summary>
        private sealed class CompiledTooltip
        {
            private readonly List<CompiledBlock> blocks;
            private readonly ValueSource<int>? maxWidth;

            internal CompiledTooltip(List<CompiledBlock> blocks, ValueSource<int>? maxWidth)
            {
                this.blocks = blocks;
                this.maxWidth = maxWidth;
            }

            /// <summary>A tooltip whose delegates evaluate in <paramref name="scope"/>.</summary>
            internal RichTooltip Create(DataScope scope)
            {
                var tooltip = new RichTooltip();
                if (maxWidth != null)
                {
                    tooltip.MaxWidth(maxWidth.Get(scope));
                }

                foreach (CompiledBlock block in blocks)
                {
                    Add(tooltip, block, scope);
                    if (block.When is { } when)
                    {
                        tooltip.WhenLast(when.IsDynamic ? () => when.Get(scope) : when.Get(scope) ? null : () => false);
                    }
                }

                return tooltip;
            }

            private static void Add(RichTooltip tooltip, CompiledBlock block, DataScope scope)
            {
                switch (block.Kind)
                {
                    case "Title":
                    case "Line":
                    {
                        Func<string> text = Getter(block.Text, scope, string.Empty);
                        ValueSource<Color>? color = block.Color;
                        if (block.Kind == "Title")
                        {
                            tooltip.Title(text);
                        }
                        else if (color != null && !color.IsDynamic)
                        {
                            tooltip.Line(text, color.Get(scope));
                        }
                        else
                        {
                            tooltip.Line(text);
                        }

                        if (color != null && (color.IsDynamic || block.Kind == "Title"))
                        {
                            tooltip.ColorLast(() => color.Get(scope));
                        }

                        break;
                    }

                    case "Icon":
                        tooltip.Icon(block.Sprite.Texture!, block.Sprite.Source, block.Scale);
                        break;
                    case "Item":
                    {
                        ValueSource<DataValue> item = block.Item!;
                        if (!item.IsDynamic && item.Get(scope).AsString() is { } id && !id.Contains(' ', StringComparison.Ordinal) && ItemRegistry.GetData(id) != null)
                        {
                            tooltip.Item(id); // a plain item id: the builder's own item row
                        }
                        else
                        {
                            tooltip.ItemInstance(() => RowScope.ItemOf(item.Get(scope))!);
                        }

                        break;
                    }

                    case "Divider":
                        tooltip.Divider();
                        break;
                    case "Money":
                    {
                        ValueSource<int>? amount = block.Amount;
                        tooltip.Money(amount == null ? () => 0 : () => amount.Get(scope));
                        break;
                    }
                }
            }

            private static Func<T> Getter<T>(ValueSource<T>? source, DataScope scope, T fallback)
            {
                if (source == null)
                {
                    return () => fallback;
                }

                if (!source.IsDynamic)
                {
                    T value = source.Get(scope);
                    return () => value;
                }

                return () => source.Get(scope);
            }
        }
    }
}
