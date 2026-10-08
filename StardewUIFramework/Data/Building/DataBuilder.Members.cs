using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Bridge;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>Element members: the type-specific members, the members every element has and styles.</summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>The members that only some element types have (set through the public interfaces).</summary>
        private void ApplyTypeMembers(IUIElement element, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            if (!ApplyLayoutMembers(element, def, scope, path, a))
            {
                ApplyControlMembers(element, def, scope, path, a);
            }
        }

        /// <summary>The type members of collections, forms and layout containers; false when <paramref name="element"/> is none of them.</summary>
        private static bool ApplyLayoutMembers(IUIElement element, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            switch (element)
            {
                case IUIList list:
                    a.Apply(def.RowHeight, ValueParsers.Int, scope, path.Field("RowHeight"), v => list.RowHeight = v);
                    a.Apply(def.VisibleRows, ValueParsers.Int, scope, path.Field("VisibleRows"), v => list.VisibleRows = v);
                    a.Apply(def.Selectable, ValueParsers.Bool, scope, path.Field("Selectable"), v => list.Selectable = v);
                    return true;
                case IUIDataGrid grid:
                    a.Apply(def.RowHeight, ValueParsers.Int, scope, path.Field("RowHeight"), v => grid.RowHeight = v);
                    a.Apply(def.VisibleRows, ValueParsers.Int, scope, path.Field("VisibleRows"), v => grid.VisibleRows = v);
                    a.Apply(def.Selectable, ValueParsers.Bool, scope, path.Field("Selectable"), v => grid.Selectable = v);
                    a.Apply(def.MultiSelect, ValueParsers.Bool, scope, path.Field("MultiSelect"), v => grid.MultiSelect = v);
                    a.Apply(def.ScrollSound, ValueParsers.Text, scope, path.Field("ScrollSound"), v => grid.ScrollSound = v);
                    a.Apply(def.SelectSound, ValueParsers.Text, scope, path.Field("SelectSound"), v => grid.SelectSound = v);
                    a.Apply(def.SortSound, ValueParsers.Text, scope, path.Field("SortSound"), v => grid.SortSound = v);
                    return true;
                case IUIForm form:
                    a.Apply(def.ShowButtons, ValueParsers.Bool, scope, path.Field("ShowButtons"), v => form.ShowButtons = v);
                    return true;
                case IUISlot slot:
                    a.Apply(def.Horizontal, ValueParsers.Bool, scope, path.Field("Horizontal"), v => slot.Horizontal = v);
                    a.Apply(def.MaxHeight, ValueParsers.OptionalInt, scope, path.Field("MaxHeight"), v => slot.MaxHeight = v);
                    a.Apply(def.MaxContributions, ValueParsers.Int, scope, path.Field("MaxContributions"), v => slot.MaxContributions = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => slot.Wrap = v);
                    return true;
                case IUIStack stack:
                    a.Apply(def.Horizontal, ValueParsers.Bool, scope, path.Field("Horizontal"), v => stack.Horizontal = v);
                    a.Apply(def.Spacing, ValueParsers.Int, scope, path.Field("Spacing"), v => stack.Spacing = v);
                    a.Apply(def.Alignment, ValueParsers.Align, scope, path.Field("Alignment"), v => stack.Alignment = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => stack.Wrap = v);
                    return true;
                case IUIGrid grid:
                    a.Apply(GridTracks(def.Columns), ValueParsers.Text, scope, path.Field("Columns"), v => grid.Columns = v);
                    a.Apply(def.Rows, ValueParsers.Text, scope, path.Field("Rows"), v => grid.Rows = v);
                    a.Apply(def.ColumnSpacing, ValueParsers.Int, scope, path.Field("ColumnSpacing"), v => grid.ColumnSpacing = v);
                    a.Apply(def.RowSpacing, ValueParsers.Int, scope, path.Field("RowSpacing"), v => grid.RowSpacing = v);
                    return true;
                case IUIPanel panel:
                    a.Apply(def.DrawBox, ValueParsers.Bool, scope, path.Field("DrawBox"), v => panel.DrawBox = v);
                    a.Apply(def.Padding, ValueParsers.Int, scope, path.Field("Padding"), v => panel.Padding = v);
                    return true;
                case IUIScrollView scroll:
                    a.Apply(def.ViewportHeight, ValueParsers.Int, scope, path.Field("ViewportHeight"), v => scroll.ViewportHeight = v);
                    a.Apply(def.ScrollStep, ValueParsers.Int, scope, path.Field("ScrollStep"), v => scroll.ScrollStep = v);
                    a.Apply(def.ShowScrollbar, ValueParsers.Bool, scope, path.Field("ShowScrollbar"), v => scroll.ShowScrollbar = v);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The type members of spacers, text, images and inputs.</summary>
        private void ApplyControlMembers(IUIElement element, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            switch (element)
            {
                case IUISpacer spacer:
                    a.Apply(def.Line, ValueParsers.Bool, scope, path.Field("Line"), v => spacer.Line = v);
                    break;
                case IUILabel label:
                    a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => label.Font = v);
                    a.Apply(def.Color, ValueParsers.ColorValue, scope, path.Field("Color"), v => label.Color = v);
                    a.Apply(def.Shadow, ValueParsers.Bool, scope, path.Field("Shadow"), v => label.Shadow = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => label.Wrap = v);
                    a.Apply(def.TextAlign, ValueParsers.Align, scope, path.Field("TextAlign"), v => label.TextAlign = v);
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => label.Scale = v);
                    a.Apply(def.RichText, ValueParsers.Bool, scope, path.Field("RichText"), v => label.RichText = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => label.Shrink = v);
                    break;
                case IUIButton button:
                    a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => button.Font = v);
                    ApplyIcon(button, def, scope, path, a);
                    a.Apply(def.IconScale, ValueParsers.Float, scope, path.Field("IconScale"), v => button.IconScale = v);
                    a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => button.ClickSound = v);
                    a.Apply(def.HoverSound, ValueParsers.Text, scope, path.Field("HoverSound"), v => button.HoverSound = v);
                    a.Apply(def.DrawBox, ValueParsers.Bool, scope, path.Field("DrawBox"), v => button.DrawBox = v);
                    a.Apply(def.RichText, ValueParsers.Bool, scope, path.Field("RichText"), v => button.RichText = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => button.Shrink = v);
                    break;
                case IUIImage image:
                    a.Apply(def.Source?.Shorthand, ValueParsers.RectangleValue, scope, path.Field("Source"), v => image.Source = v);
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => image.Scale = v);
                    a.Apply(def.Tint, ValueParsers.ColorValue, scope, path.Field("Tint"), v => image.Tint = v);
                    break;
                case IUIItemImage itemImage:
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => itemImage.Scale = v);
                    a.Apply(def.Stack, ValueParsers.ItemStack, scope, path.Field("Stack"), v => itemImage.Stack = v);
                    a.Apply(def.DrawShadow, ValueParsers.Bool, scope, path.Field("DrawShadow"), v => itemImage.DrawShadow = v);
                    a.Apply(def.Alpha, ValueParsers.Float, scope, path.Field("Alpha"), v => itemImage.Alpha = v);
                    a.Apply(def.Tint, ValueParsers.ColorValue, scope, path.Field("Tint"), v => itemImage.Tint = v);
                    break;
                case IUICheckbox checkbox:
                    Func<string>? checkboxLabel = a.Text(def.Label, scope, path.Field("Label"));
                    if (checkboxLabel != null)
                    {
                        checkbox.Label = checkboxLabel;
                    }

                    a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => checkbox.ClickSound = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => checkbox.Shrink = v);
                    break;
                case IUITextInput text:
                    Func<string>? placeholder = a.Text(def.Placeholder, scope, path.Field("Placeholder"));
                    if (placeholder != null)
                    {
                        text.Placeholder = placeholder;
                    }

                    a.Apply(def.MaxLength, ValueParsers.Int, scope, path.Field("MaxLength"), v => text.MaxLength = v);
                    ApplyTexture(def.Texture, scope, path.Field("Texture"), a, t => text.Texture = t);
                    break;
                case IUINumberInput number:
                    // Min / Max / Step / Clamp were passed to the constructor; live values are re-applied here
                    a.Apply(def.Min, ValueParsers.Number, scope, path.Field("Min"), v => number.Min = v);
                    a.Apply(def.Max, ValueParsers.Number, scope, path.Field("Max"), v => number.Max = v);
                    a.Apply(def.Step, ValueParsers.Number, scope, path.Field("Step"), v => number.Step = v);
                    a.Apply(def.Clamp, ValueParsers.Bool, scope, path.Field("Clamp"), v => number.Clamp = v);
                    a.Apply(def.Decimals, ValueParsers.Int, scope, path.Field("Decimals"), v => number.Decimals = v);
                    ApplyTexture(def.Texture, scope, path.Field("Texture"), a, t => number.Texture = t);
                    break;
                case IUIDropdown dropdown:
                    a.Apply(def.MaxVisible, ValueParsers.Int, scope, path.Field("MaxVisible"), v => dropdown.MaxVisible = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => dropdown.Shrink = v);
                    break;
                case IUISlider slider:
                    a.Apply(def.Min, ValueParsers.Number, scope, path.Field("Min"), v => slider.Min = v);
                    a.Apply(def.Max, ValueParsers.Number, scope, path.Field("Max"), v => slider.Max = v);
                    a.Apply(def.Step, ValueParsers.Number, scope, path.Field("Step"), v => slider.Step = v);
                    break;
            }
        }

        /// <summary>A Grid's column tracks as the grid parses them ("auto, *, 120"): the widths of its column list.</summary>
        private static string? GridTracks(List<ColumnDefinition>? columns)
        {
            if (columns == null || columns.Count == 0)
            {
                return null;
            }

            return string.Join(", ", columns.Select(c => string.IsNullOrWhiteSpace(c?.Width) ? "*" : c!.Width!.Trim()));
        }

        private void ApplyIcon(IUIButton button, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            a.Apply(def.Icon, ValueParsers.Text, scope, path.Field("Icon"), reference =>
            {
                if (!sprites.TryResolve(reference, scope.Owner, out SpriteRef sprite, out string error))
                {
                    a.Log.Error(path.Field("Icon"), error);
                    return;
                }

                button.Icon = sprite.Texture!;
                button.IconSource = sprite.Source;
                if (sprite.Scale.HasValue)
                {
                    button.IconScale = sprite.Scale.Value;
                }
            });
        }

        private void ApplyTexture(string? raw, DataScope scope, DataPath path, PropertyApplier a, Action<Microsoft.Xna.Framework.Graphics.Texture2D> apply)
        {
            a.Apply(raw, ValueParsers.Text, scope, path, reference =>
            {
                if (sprites.TryResolve(reference, scope.Owner, out SpriteRef sprite, out string error))
                {
                    apply(sprite.Texture!);
                }
                else
                {
                    a.Log.Error(path, error);
                }
            });
        }

        /// <summary>Members every element has.</summary>
        private void ApplyCommon(BuildContext ctx, IUIElement e, ElementDefinition def, DataScope scope, DataPath path, Visibility visibility)
        {
            PropertyApplier a = ctx.Applier;
            a.Apply(def.Visible, ValueParsers.Bool, scope, path.Field("Visible"), v => visibility.SetRequested(v));
            a.Apply(def.Enabled, ValueParsers.Bool, scope, path.Field("Enabled"), v => e.Enabled = v);
            Func<string>? tooltip = a.Text(def.Tooltip, scope, path.Field("Tooltip"));
            if (tooltip != null)
            {
                e.Tooltip = tooltip;
            }

            Func<string>? tooltipTitle = a.Text(def.TooltipTitle, scope, path.Field("TooltipTitle"));
            if (tooltipTitle != null)
            {
                e.TooltipTitle = tooltipTitle;
            }

            if (def.RichTooltip != null)
            {
                CompiledTooltip? rich = CompileTooltip(def.RichTooltip, scope.Owner, path.Field("RichTooltip"), a);
                if (rich != null)
                {
                    e.RichTooltip = rich.Create(scope);
                }
            }

            a.Apply(def.Tag, ValueParsers.Text, scope, path.Field("Tag"), v => e.Tag = v);
            a.Apply(def.Sealed, ValueParsers.Bool, scope, path.Field("Sealed"), v => e.Sealed = v);
            Func<string>? accessibleName = a.Text(def.AccessibleName, scope, path.Field("AccessibleName"));
            if (accessibleName != null)
            {
                e.AccessibleName = accessibleName;
            }

            a.Apply(def.Margin, ValueParsers.Margin, scope, path.Field("Margin"), v => e.SetMargin(v[0], v[1], v[2], v[3]));
            a.Apply(def.MarginLeft, ValueParsers.Int, scope, path.Field("MarginLeft"), v => e.MarginLeft = v);
            a.Apply(def.MarginTop, ValueParsers.Int, scope, path.Field("MarginTop"), v => e.MarginTop = v);
            a.Apply(def.MarginRight, ValueParsers.Int, scope, path.Field("MarginRight"), v => e.MarginRight = v);
            a.Apply(def.MarginBottom, ValueParsers.Int, scope, path.Field("MarginBottom"), v => e.MarginBottom = v);
            a.Apply(def.Width, ValueParsers.OptionalInt, scope, path.Field("Width"), v => e.Width = v);
            a.Apply(def.Height, ValueParsers.OptionalInt, scope, path.Field("Height"), v => e.Height = v);
            a.Apply(def.MinWidth, ValueParsers.OptionalInt, scope, path.Field("MinWidth"), v => e.MinWidth = v);
            a.Apply(def.MaxWidth, ValueParsers.OptionalInt, scope, path.Field("MaxWidth"), v => e.MaxWidth = v);
            a.Apply(def.HorizontalAlign, ValueParsers.Align, scope, path.Field("HorizontalAlign"), v => e.HorizontalAlign = v);
            a.Apply(def.VerticalAlign, ValueParsers.Align, scope, path.Field("VerticalAlign"), v => e.VerticalAlign = v);
            a.Apply(def.X, ValueParsers.Int, scope, path.Field("X"), v => e.X = v);
            a.Apply(def.Y, ValueParsers.Int, scope, path.Field("Y"), v => e.Y = v);
            a.Apply(def.Cell, ValueParsers.Pair, scope, path.Field("Cell"), v =>
            {
                e.Row = v.X;
                e.Column = v.Y;
            });
            a.Apply(def.Span, ValueParsers.Pair, scope, path.Field("Span"), v =>
            {
                e.RowSpan = v.X;
                e.ColumnSpan = v.Y;
            });
            a.Apply(def.Row, ValueParsers.Int, scope, path.Field("Row"), v => e.Row = v);
            a.Apply(def.Column, ValueParsers.Int, scope, path.Field("Column"), v => e.Column = v);
            a.Apply(def.RowSpan, ValueParsers.Int, scope, path.Field("RowSpan"), v => e.RowSpan = v);
            a.Apply(def.ColumnSpan, ValueParsers.Int, scope, path.Field("ColumnSpan"), v => e.ColumnSpan = v);

            ApplyDrawHooks(e, def, scope, path, a.Log);

            StyleDefinition? style = EffectiveStyle(def, scope.Owner, path, a.Log);
            if (style != null)
            {
                e.Style = BuildStyle(ctx.Api, style, scope, path.Field("Style"), a);
            }
        }

        /// <summary>The element's style: its <c>Class</c>es (from the owner's <c>Owners</c> entry) merged in order, then the inline <c>Style</c>.</summary>
        private StyleDefinition? EffectiveStyle(ElementDefinition def, string owner, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(def.Class))
            {
                return def.Style;
            }

            Dictionary<string, StyleDefinition>? classes = owners(owner)?.Classes;
            StyleDefinition merged = new();
            foreach (string name in def.Class.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StyleDefinition? found = classes?.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                if (found == null)
                {
                    string? suggestion = classes == null ? null : DataValidator.Suggest(name, classes.Keys);
                    log.Warn(path.Field("Class"), $"'{owner}' has no style class '{name}' in {DataAssets.Owners}{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    continue;
                }

                MergeStyle(merged, found);
            }

            if (def.Style != null)
            {
                MergeStyle(merged, def.Style);
            }

            return merged;
        }

        /// <summary>Copy every member <paramref name="over"/> sets onto <paramref name="target"/>.</summary>
        internal static void MergeStyle(StyleDefinition target, StyleDefinition over)
        {
            foreach (System.Reflection.PropertyInfo property in DataValidator.ModelProperties(typeof(StyleDefinition)))
            {
                object? value = property.GetValue(over);
                if (value != null)
                {
                    property.SetValue(target, value);
                }
            }
        }

        /// <summary>Build an <see cref="IUIStyle"/> from a style definition (also used for the owner's DefaultStyle).</summary>
        internal IUIStyle BuildStyle(StardewUIApi api, StyleDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            IUIStyle style = api.CreateStyle();
            a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => style.Font = v);
            a.Apply(def.TextColor, ValueParsers.ColorValue, scope, path.Field("TextColor"), v => style.TextColor = v);
            a.Apply(def.HoverColor, ValueParsers.ColorValue, scope, path.Field("HoverColor"), v => style.HoverColor = v);
            ApplyTexture(def.BoxTexture, scope, path.Field("BoxTexture"), a, t => style.BoxTexture = t);
            if (def.BoxTexture != null && def.BoxSource == null && sprites.TryResolve(def.BoxTexture, scope.Owner, out SpriteRef sprite, out _))
            {
                style.BoxSource = sprite.BoxSource ?? sprite.Source;
            }

            a.Apply(def.BoxSource, ValueParsers.RectangleValue, scope, path.Field("BoxSource"), v => style.BoxSource = v);
            a.Apply(def.BoxScale, ValueParsers.Float, scope, path.Field("BoxScale"), v => style.BoxScale = v);
            a.Apply(def.Padding, ValueParsers.Int, scope, path.Field("Padding"), v => style.Padding = v);
            a.Apply(def.TextShadow, ValueParsers.Bool, scope, path.Field("TextShadow"), v => style.TextShadow = v);
            a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => style.ClickSound = v);
            a.Apply(def.HoverSound, ValueParsers.Text, scope, path.Field("HoverSound"), v => style.HoverSound = v);
            return style;
        }
    }
}
