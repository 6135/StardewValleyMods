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
            string id = FormFieldId(def, index);
            address = FormFieldAddress(ctx, def, id, scope, path);
            if (!TryFormFieldKind(ctx, def, path, out FormFieldKind fieldKind, out Type type, out DataValue fallback))
            {
                return null;
            }

            // the default (only used while the value does not exist)
            StateAddress target = address;
            SetFormFieldDefault(a, def, target, fallback, scope, path);

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

            ApplyFormFieldOptions(ctx, property, def, scope, path);
            return property;
        }

        /// <summary>The field's id: <c>Id</c>, else the last segment of <c>Bind</c>, else <c>field&lt;index&gt;</c>.</summary>
        private static string FormFieldId(FormFieldDefinition def, int index)
        {
            string? id = def.Id?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                id = def.Bind != null ? def.Bind.Trim().Split('.', '[', ']').LastOrDefault(p => p.Length > 0) : null;
            }

            if (string.IsNullOrEmpty(id))
            {
                id = "field" + index.ToString(CultureInfo.InvariantCulture);
            }

            return id;
        }

        /// <summary>The state value the field edits: <c>Bind</c>, else the menu's state value named by the field id.</summary>
        private static StateAddress FormFieldAddress(BuildContext ctx, FormFieldDefinition def, string id, DataScope scope, DataPath path)
        {
            var address = new StateAddress(StateScope.Menu, scope.StateKey ?? scope.MenuKey, id);
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

            return address;
        }

        /// <summary>The field's <c>Kind</c> as a form field kind, CLR type and default value; false (logged) for an unknown kind.</summary>
        private static bool TryFormFieldKind(BuildContext ctx, FormFieldDefinition def, DataPath path, out FormFieldKind fieldKind, out Type type, out DataValue fallback)
        {
            string kind = (def.Kind ?? (def.Choices is { Count: > 0 } ? "Dropdown" : "Text")).Trim().ToLowerInvariant();
            switch (kind)
            {
                case "checkbox":
                    (fieldKind, type, fallback) = (FormFieldKind.Bool, typeof(bool), DataValue.False);
                    return true;
                case "number":
                    (fieldKind, type, fallback) = (FormFieldKind.Number, typeof(double), DataValue.Zero);
                    return true;
                case "integer":
                    (fieldKind, type, fallback) = (FormFieldKind.Number, typeof(int), DataValue.Zero);
                    return true;
                case "dropdown":
                    (fieldKind, type, fallback) = (FormFieldKind.Choice, typeof(string), DataValue.FromString(def.Choices?.FirstOrDefault() ?? string.Empty));
                    return true;
                case "text":
                    (fieldKind, type, fallback) = (FormFieldKind.Text, typeof(string), DataValue.EmptyString);
                    return true;
                default:
                    ctx.Log.Error(path.Field("Kind"), $"'{def.Kind}' is not Checkbox, Number, Integer, Text or Dropdown; the field is skipped.");
                    (fieldKind, type, fallback) = (default, typeof(string), DataValue.EmptyString);
                    return false;
            }
        }

        /// <summary>Register the field's default: <c>Value</c>, else the kind's fallback (unless the value already has a default).</summary>
        private void SetFormFieldDefault(PropertyApplier a, FormFieldDefinition def, StateAddress target, DataValue fallback, DataScope scope, DataPath path)
        {
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
        }

        /// <summary>The field's label, tooltip, number range, choices check and <c>Validate</c> expression.</summary>
        private void ApplyFormFieldOptions(BuildContext ctx, FormProperty property, FormFieldDefinition def, DataScope scope, DataPath path)
        {
            PropertyApplier a = ctx.Applier;

            // the caption reads Label every frame, so a live label stays live
            a.Apply(def.Label, ValueParsers.Text, scope, path.Field("Label"), v => property.Label = v);
            if (def.Tooltip != null)
            {
                property.Tooltip = a.Initial(def.Tooltip, ValueParsers.Text, string.Empty, scope, path.Field("Tooltip"));
            }

            if (property.Kind == FormFieldKind.Number)
            {
                (double typeMin, double typeMax) = property.Type == typeof(int) ? (int.MinValue, int.MaxValue) : (-DataDefaults.NumberInput.Max, DataDefaults.NumberInput.Max);
                property.Min = a.Initial(def.Min, ValueParsers.Number, typeMin, scope, path.Field("Min"));
                property.Max = a.Initial(def.Max, ValueParsers.Number, typeMax, scope, path.Field("Max"));
                if (property.Max < property.Min)
                {
                    (property.Min, property.Max) = (property.Max, property.Min);
                }
            }

            if (property.Kind == FormFieldKind.Choice && property.Choices.Length == 0)
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
        }

        /// <summary>Re-reads a form's controls when one of its state values changed from outside the form.</summary>
        private sealed class FormSync : IDataRefresher
        {
            private readonly IUIForm form;
            private readonly List<StateAddress> addresses;
            private readonly DataStateStore store;
            private readonly WriteFlag writing;
            private readonly DataValue[] last;

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
                // compare in place (runs every tick)
                bool changed = false;
                for (int i = 0; i < addresses.Count; i++)
                {
                    DataValue now = store.Read(addresses[i]);
                    if (!now.Equals(last[i]))
                    {
                        last[i] = now;
                        changed = true;
                    }
                }

                if (changed && !writing.Value)
                {
                    form.Refresh();
                }

                writing.Value = false;
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
