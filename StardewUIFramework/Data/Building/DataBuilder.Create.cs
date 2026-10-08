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
    /// <summary>Element creation: each element type through the facade with its construction arguments, inputs bound to their values.</summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>Create the element through the facade with its construction arguments.</summary>
        private IUIElement Create(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            StardewUIApi api = ctx.Api;
            PropertyApplier applier = ctx.Applier;
            string id = ctx.IdOf(def);
            switch (def.Type)
            {
                case ElementTypes.Stack:
                    return api.AddStack(parent, id, DataDefaults.Stack.Horizontal, DataDefaults.Stack.Spacing);
                case ElementTypes.Repeat:
                    return api.AddStack(parent, id, DataDefaults.Repeat.Horizontal, DataDefaults.Repeat.Spacing);
                case ElementTypes.List:
                    return CreateList(ctx, parent, def, scope, path);
                case ElementTypes.DataGrid:
                    return CreateDataGrid(ctx, parent, def, scope, path);
                case ElementTypes.Form:
                    return def.Model != null ? CreateModelForm(ctx, parent, def, scope, path) : CreateForm(ctx, parent, def, scope, path);
                case ElementTypes.Composite:
                    return CreateComposite(ctx, parent, def, scope, path);
                case ElementTypes.Template:
                    return CreateTemplate(ctx, parent, def, scope, path);
                case ElementTypes.Outlet:
                    return api.AddStack(parent, id, DataDefaults.Outlet.Horizontal, DataDefaults.Outlet.Spacing);
                case ElementTypes.Grid:
                    return api.AddGrid(parent, id, DataDefaults.Grid.Columns, def.Rows ?? DataDefaults.Grid.Rows); // the column tracks are applied (possibly live) with the type members
                case ElementTypes.Panel:
                    return api.AddPanel(parent, id, DataDefaults.Panel.DrawBox, DataDefaults.Panel.Padding);
                case ElementTypes.Canvas:
                    return api.AddCanvas(parent, id);
                case ElementTypes.ScrollView:
                    return api.AddScrollView(parent, id, DataDefaults.ScrollView.ViewportHeight);
                case ElementTypes.Slot:
                    return api.AddSlot(parent, id);
                case ElementTypes.Spacer:
                    return api.AddSpacer(parent, id, DataDefaults.Spacer.Width, DataDefaults.Spacer.Height);
                case ElementTypes.Label:
                    return api.AddLabel(parent, id, applier.Text(def.Text, scope, path.Field("Text")) ?? (() => string.Empty));
                case ElementTypes.Button:
                    return api.AddButton(parent, id, applier.Text(def.Text, scope, path.Field("Text")) ?? (() => string.Empty), null!);
                case ElementTypes.Image:
                    return CreateImage(api, parent, id, def, scope, path, applier);
                case ElementTypes.ItemImage:
                    return CreateItemImage(api, parent, id, def, scope, path, applier);
                default:
                    return CreateInput(ctx, parent, id, def, scope, path);
            }
        }

        /// <summary>Create an input element (<c>Checkbox</c>, <c>TextInput</c>, <c>NumberInput</c>, <c>Dropdown</c>, <c>Slider</c>) bound to its value.</summary>
        private IUIElement CreateInput(BuildContext ctx, IUIContainer parent, string id, ElementDefinition def, DataScope scope, DataPath path)
        {
            StardewUIApi api = ctx.Api;
            PropertyApplier applier = ctx.Applier;
            switch (def.Type)
            {
                case ElementTypes.Checkbox:
                {
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Bool, false, DataValue.FromBool);
                    return api.AddCheckbox(parent, id, () => target.Read().AsBool(), v => Write(scope, target, DataValue.FromBool(v)));
                }
                case ElementTypes.TextInput:
                {
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Text, string.Empty, DataValue.FromString);
                    return api.AddTextInput(parent, id, () => target.Read().AsString(), v => Write(scope, target, DataValue.FromString(v ?? string.Empty)));
                }
                case ElementTypes.NumberInput:
                {
                    double min = applier.Initial(def.Min, ValueParsers.Number, DataDefaults.NumberInput.Min, scope, path.Field("Min"));
                    double max = applier.Initial(def.Max, ValueParsers.Number, DataDefaults.NumberInput.Max, scope, path.Field("Max"));
                    double step = applier.Initial(def.Step, ValueParsers.Number, DataDefaults.NumberInput.Step, scope, path.Field("Step"));
                    bool clamp = applier.Initial(def.Clamp, ValueParsers.Bool, DataDefaults.NumberInput.Clamp, scope, path.Field("Clamp"));
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Number, min, DataValue.FromNumber);
                    return api.AddNumberInput(parent, id, () => target.Read().AsNumber(), v => Write(scope, target, DataValue.FromNumber(v)), min, max, step, clamp);
                }
                case ElementTypes.Dropdown:
                {
                    if (def.ChoicesSource != null)
                    {
                        return CreateSourcedDropdown(ctx, parent, def, scope, path);
                    }

                    string[] choices = def.Choices?.ToArray() ?? Array.Empty<string>();
                    string[]? labels = def.Labels == null ? null : LabelsFor(choices, def.Labels);
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Text, choices.Length > 0 ? choices[0] : string.Empty, DataValue.FromString);
                    return api.AddDropdown(parent, id, () => choices, labels == null ? null! : () => labels, () => target.Read().AsString(), v => Write(scope, target, DataValue.FromString(v ?? string.Empty)));
                }
                case ElementTypes.Slider:
                {
                    double min = applier.Initial(def.Min, ValueParsers.Number, DataDefaults.Slider.Min, scope, path.Field("Min"));
                    double max = applier.Initial(def.Max, ValueParsers.Number, DataDefaults.Slider.Max, scope, path.Field("Max"));
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Number, min, DataValue.FromNumber);
                    return api.AddSlider(parent, id, () => target.Read().AsNumber(), v => Write(scope, target, DataValue.FromNumber(v)), min, max);
                }
                default:
                    throw new InvalidOperationException($"unsupported element type '{def.Type}'.");
            }
        }

        /// <summary>
        /// The value an input reads and writes: its <c>Bind</c> (default <c>menu.&lt;Id&gt;</c>), a state value or (v1.6)
        /// a member of a model exposed from C# (<c>model.settings.Day</c>). For state values the input's <c>Value</c>
        /// becomes the value's default (only used while the value does not exist); without one, the type's fallback is
        /// the default unless another default (the menu's <c>State</c>) is already registered. A model provides its own value.
        /// </summary>
        private BindTarget BindInput<T>(BuildContext ctx, ElementDefinition def, DataScope scope, DataPath path, ValueKind<T> kind, T fallback, Func<T, DataValue> wrap)
        {
            StateAddress address = new(StateScope.Menu, scope.StateKey ?? scope.MenuKey, ctx.InstanceState ? ctx.IdOf(def) : def.Id!);
            if (def.Bind != null)
            {
                if (BindTarget.TryParse(def.Bind, scope, allowBare: true, out BindTarget? bound, out string error))
                {
                    if (!bound.IsState)
                    {
                        if (def.Value != null)
                        {
                            ctx.Log.Info(path.Field("Value"), $"the input is bound to {bound}, which provides its own value; Value is ignored.");
                        }

                        return bound;
                    }

                    address = bound.Address;
                }
                else
                {
                    ctx.Log.Error(path.Field("Bind"), $"{error} The input uses {address} instead.");
                }
            }

            ValueSource<T>? initial = def.Value == null ? null : ctx.Applier.Source(def.Value, kind, path.Field("Value"));
            if (initial != null || !store.HasDefault(address))
            {
                store.SetDefault(address, () => wrap(initial == null ? fallback : initial.Get(scope)));
            }

            return BindTarget.ForState(address);
        }

        /// <summary>Write an input's value into the state store or model (a failed write, e.g. <c>player.*</c> on the title screen, is logged).</summary>
        private static void Write(DataScope scope, BindTarget target, DataValue value)
        {
            if (!target.Write(value, out string error))
            {
                UIServices.Log($"[{scope.Owner}] {scope}: could not write {target}: {error}", StardewModdingAPI.LogLevel.Warn);
            }
        }

        /// <summary>Labels padded with the choice values so both lists have the same length.</summary>
        private static string[] LabelsFor(string[] choices, List<string> labels)
        {
            var result = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                result[i] = i < labels.Count ? labels[i] : choices[i];
            }

            return result;
        }

        /// <summary>
        /// An image whose <c>Sprite</c> is a value like any other field: a literal reference, or a live <c>${...}</c>
        /// giving a reference string, a <see cref="Texture2D"/> or a <c>Tuple&lt;Texture2D, Rectangle&gt;</c> (a C# row's
        /// sprite), so a row template can show a different image per row. The sprite's own source / scale / tint apply
        /// unless the element sets <c>Source</c> / <c>Scale</c> / <c>Tint</c> itself.
        /// </summary>
        private IUIElement CreateImage(StardewUIApi api, IUIContainer parent, string id, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier applier)
        {
            IUIImage image = api.AddImage(parent, id, null!, null, DataDefaults.Image.Scale);
            if (string.IsNullOrWhiteSpace(def.Sprite))
            {
                applier.Log.Warn(path.Field("Sprite"), "an Image needs a Sprite (sprite:Owner/name, item:(O)24 or asset:Path@x,y,w,h); it draws nothing.");
                return image;
            }

            DataPath spritePath = path.Field("Sprite");
            bool ownSource = def.Source?.Shorthand != null, ownScale = def.Scale != null, ownTint = def.Tint != null;
            string? lastText = null;
            applier.Apply(def.Sprite.Trim(), ValueParsers.Raw, scope, spritePath, value =>
            {
                string? text = value.Kind == DataKind.String ? value.AsString() : null;
                if (text != null && text == lastText)
                {
                    return; // same reference: already applied
                }

                lastText = text;
                if (!TryResolveSprite(value, scope.Owner, out SpriteRef sprite, out string error))
                {
                    applier.Log.Error(spritePath, error);
                    return;
                }

                image.Texture = sprite.Texture!;
                if (!ownSource)
                {
                    image.Source = sprite.Source;
                }

                if (!ownScale && sprite.Scale.HasValue)
                {
                    image.Scale = sprite.Scale.Value;
                }

                if (!ownTint && sprite.Tint.HasValue)
                {
                    image.Tint = sprite.Tint.Value;
                }
            });

            return image;
        }

        /// <summary>A sprite value: a reference string (<see cref="SpriteRefs"/>), a texture, or a (texture, source) tuple.</summary>
        private bool TryResolveSprite(DataValue value, string owner, out SpriteRef sprite, out string error)
        {
            error = string.Empty;
            switch (value.AsObject())
            {
                case Texture2D texture:
                    sprite = new SpriteRef(texture, null, null, null, null);
                    return true;
                case Tuple<Texture2D, Rectangle> pair:
                    sprite = new SpriteRef(pair.Item1, pair.Item2, null, null, null);
                    return true;
                case ValueTuple<Texture2D, Rectangle> pair:
                    sprite = new SpriteRef(pair.Item1, pair.Item2, null, null, null);
                    return true;
                default:
                    if (value.IsNull || value.AsString().Trim().Length == 0)
                    {
                        sprite = default;
                        error = "the Sprite value is empty.";
                        return false;
                    }

                    return sprites.TryResolve(value.AsString(), owner, out sprite, out error);
            }
        }

        /// <summary>
        /// An item image whose <c>Item</c> is a qualified item id, an item query (<c>FLAVORED_ITEM Wine (O)398</c>) or an
        /// expression giving an item (<c>${row.item}</c>) or item text. Items created from text are cached by
        /// (text, count, quality), so a live value that keeps giving the same text never re-creates the item.
        /// </summary>
        private static IUIElement CreateItemImage(StardewUIApi api, IUIContainer parent, string id, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier applier)
        {
            DataValue value = DataValue.Null;
            int count = DataDefaults.ItemImage.Count;
            int quality = DataDefaults.ItemImage.Quality;
            applier.Apply(def.Count, ValueParsers.Int, scope, path.Field("Count"), v => count = Math.Max(1, v));
            applier.Apply(def.Quality, ValueParsers.Int, scope, path.Field("Quality"), v => quality = Math.Clamp(v, 0, 4));

            // the getter goes through the item cache (a dictionary hit once created), so an item that cannot be created
            // yet (an item query on the title screen) is retried instead of staying empty
            IUIItemImage image = api.AddItemImage(parent, id, () => RowScope.ItemOf(value, count, quality)!, DataDefaults.ItemImage.Scale);
            if (!string.IsNullOrWhiteSpace(def.Item))
            {
                applier.Apply(def.Item.Trim(), ValueParsers.Raw, scope, path.Field("Item"), v => value = v);
            }

            return image;
        }
    }
}
