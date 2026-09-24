using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// <c>Form</c>: an auto-form whose fields are state values. Each field becomes an accessor-backed
    /// <see cref="FormProperty"/> (the getter reads the state value, the setter writes it), so the form keeps every
    /// auto-form behavior: the snapshot for Cancel, undo / redo, dirty tracking (<c>el[id].dirty</c>,
    /// <c>canUndo</c>, <c>canRedo</c>), the button row and validation messages (<c>Validate</c> expressions). A state
    /// change from elsewhere (an action, another input) re-reads the controls.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>The model object of a data form (the fields read and write state; it only identifies the form).</summary>
        internal sealed class DataFormModel
        {
            internal DataFormModel(string key)
            {
                Key = key;
            }

            /// <summary>The <c>owner/menu#form</c> key.</summary>
            internal string Key { get; }

            public override string ToString() => Key;
        }

        private IUIElement CreateForm(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            string id = ctx.IdOf(def);
            var properties = new List<FormProperty>();
            var addresses = new List<StateAddress>();
            var writing = new WriteFlag();
            if (def.Fields == null || def.Fields.Count == 0)
            {
                ctx.Log.Warn(path.Field("Fields"), "a Form needs Fields ([{ \"Id\": \"volume\", \"Kind\": \"Number\", \"Bind\": \"config.volume\" }, ...]).");
            }
            else
            {
                for (int i = 0; i < def.Fields.Count; i++)
                {
                    FormFieldDefinition? field = def.Fields[i];
                    if (field == null)
                    {
                        continue;
                    }

                    FormProperty? property = CreateFormField(ctx, field, i, scope, path.Field("Fields").Index(i, field.Id), writing, out StateAddress address);
                    if (property != null)
                    {
                        properties.Add(property);
                        addresses.Add(address);
                    }
                }
            }

            IUIForm form = ctx.Api.AddFormInternal(parent, id, new DataFormModel(scope.MenuKey + "#" + id), properties);
            ctx.Applier.AddRefresher(new FormSync(form, addresses, store, writing));
            return form;
        }

        /// <summary>Set when the form itself wrote a field since the last sync (its own edits do not re-read the controls).</summary>
        private sealed class WriteFlag
        {
            internal bool Value { get; set; }
        }

        private FormProperty? CreateFormField(BuildContext ctx, FormFieldDefinition def, int index, DataScope scope, DataPath path, WriteFlag writing, out StateAddress address)
        {
            PropertyApplier a = ctx.Applier;
            string? id = def.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                id = def.Bind != null ? def.Bind.Trim().Split('.', '[', ']').LastOrDefault(p => p.Length > 0) : null;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = "field" + index.ToString(CultureInfo.InvariantCulture);
            }

            address = new StateAddress(StateScope.Menu, scope.StateKey ?? scope.MenuKey, id);
            if (def.Bind != null)
            {
                if (StateAddress.TryParse(def.Bind, scope, allowBare: true, out StateAddress bound, out string error))
                {
                    address = bound;
                }
                else
                {
                    ctx.Log.Error(path.Field("Bind"), $"{error} The field uses {address} instead.");
                }
            }

            string kind = (def.Kind ?? (def.Choices is { Count: > 0 } ? "Dropdown" : "Text")).Trim().ToLowerInvariant();
            FormFieldKind fieldKind;
            Type type;
            DataValue fallback;
            switch (kind)
            {
                case "checkbox":
                case "bool":
                    (fieldKind, type, fallback) = (FormFieldKind.Bool, typeof(bool), DataValue.False);
                    break;
                case "number":
                    (fieldKind, type, fallback) = (FormFieldKind.Number, typeof(double), DataValue.Zero);
                    break;
                case "integer":
                    (fieldKind, type, fallback) = (FormFieldKind.Number, typeof(int), DataValue.Zero);
                    break;
                case "dropdown":
                    (fieldKind, type, fallback) = (FormFieldKind.Choice, typeof(string), DataValue.FromString(def.Choices?.FirstOrDefault() ?? string.Empty));
                    break;
                case "text":
                    (fieldKind, type, fallback) = (FormFieldKind.Text, typeof(string), DataValue.EmptyString);
                    break;
                default:
                    ctx.Log.Error(path.Field("Kind"), $"'{def.Kind}' is not Checkbox, Number, Integer, Text or Dropdown; the field is skipped.");
                    return null;
            }

            // the default (only used while the value does not exist)
            StateAddress target = address;
            if (def.Value != null)
            {
                ValueSource<string>? initial = a.Source(def.Value, ValueParsers.Text, path.Field("Value"));
                if (initial != null)
                {
                    store.SetDefault(target, () => StateAddress.Infer(initial.Get(scope)));
                }
            }
            else if (!store.HasDefault(target))
            {
                store.SetDefault(target, () => fallback);
            }

            Func<object, object?> getter = fieldKind switch
            {
                FormFieldKind.Bool => _ => store.Read(target).AsBool(),
                FormFieldKind.Number when type == typeof(int) => _ => (int)Math.Round(store.Read(target).AsNumber(), MidpointRounding.AwayFromZero),
                FormFieldKind.Number => _ => store.Read(target).AsNumber(),
                _ => _ => store.Read(target).AsString()
            };
            Action<object, object?> setter = (_, value) =>
            {
                writing.Value = true; // the sync takes the form's own writes without re-reading the controls
                Write(scope, Bridge.BindTarget.ForState(target), value switch
                {
                    bool flag => DataValue.FromBool(flag),
                    int whole => DataValue.FromNumber(whole),
                    double number => DataValue.FromNumber(number),
                    _ => DataValue.FromString(value?.ToString() ?? string.Empty)
                });
            };

            var property = new FormProperty(id, type, fieldKind, getter, setter)
            {
                Section = def.Section != null ? a.Initial(def.Section, ValueParsers.Text, string.Empty, scope, path.Field("Section")) : null,
                ReadOnly = a.Initial(def.ReadOnly, ValueParsers.Bool, false, scope, path.Field("ReadOnly")),
                Choices = def.Choices?.ToArray() ?? Array.Empty<string>()
            };

            // the caption reads Label every frame, so a live label stays live
            FormProperty captioned = property;
            a.Apply(def.Label, ValueParsers.Text, scope, path.Field("Label"), v => captioned.Label = v);
            if (def.Tooltip != null)
            {
                property.Tooltip = a.Initial(def.Tooltip, ValueParsers.Text, string.Empty, scope, path.Field("Tooltip"));
            }

            if (fieldKind == FormFieldKind.Number)
            {
                (double typeMin, double typeMax) = type == typeof(int) ? (int.MinValue, int.MaxValue) : (-DefaultNumberMax, DefaultNumberMax);
                property.Min = a.Initial(def.Min, ValueParsers.Number, typeMin, scope, path.Field("Min"));
                property.Max = a.Initial(def.Max, ValueParsers.Number, typeMax, scope, path.Field("Max"));
                if (property.Max < property.Min)
                {
                    (property.Min, property.Max) = (property.Max, property.Min);
                }
            }

            if (fieldKind == FormFieldKind.Choice && property.Choices.Length == 0)
            {
                ctx.Log.Warn(path.Field("Choices"), "a Dropdown field needs Choices.");
            }

            if (def.Validate != null)
            {
                string validate = def.Validate;
                property.CustomValidator = (_, value) =>
                {
                    var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase) { ["value"] = DataValue.FromObject(value) };
                    DataValue result = resolver.Evaluate(validate, scope.WithEvent("Validate", null, fields), out string? error);
                    return error != null ? null : ValidationMessage(result);
                };
            }

            return property;
        }

        /// <summary>Re-reads a form's controls when one of its state values changed from outside the form.</summary>
        private sealed class FormSync : IDataRefresher
        {
            private readonly IUIForm form;
            private readonly List<StateAddress> addresses;
            private readonly DataStateStore store;
            private readonly WriteFlag writing;
            private DataValue[] last;

            internal FormSync(IUIForm form, List<StateAddress> addresses, DataStateStore store, WriteFlag writing)
            {
                this.form = form;
                this.addresses = addresses;
                this.store = store;
                this.writing = writing;
                last = Read();
            }

            public void Refresh(bool opening)
            {
                DataValue[] now = Read();
                if (!now.SequenceEqual(last) && !writing.Value)
                {
                    form.Refresh();
                }

                writing.Value = false;
                last = now;
            }

            private DataValue[] Read() => addresses.Select(store.Read).ToArray();
        }

        /// <summary>The form events: <c>OnSaved</c>, <c>OnCancelled</c>, <c>OnChanged</c>.</summary>
        private static void WireFormEvents(IUIForm form, ElementDefinition def, DataScope scope)
        {
            SetIf(DataActionRunner.Handler<IUIForm>(def.OnSaved, scope, "OnSaved"), h => form.OnSaved = h);
            SetIf(DataActionRunner.Handler<IUIForm>(def.OnCancelled, scope, "OnCancelled"), h => form.OnCancelled = h);
            SetIf(DataActionRunner.Handler<IUIForm>(def.OnChanged, scope, "OnChanged"), h => form.OnChanged = h);
        }
    }
}
