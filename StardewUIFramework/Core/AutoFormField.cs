using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Components;

namespace UIFramework.Core
{
    /// <summary>
    /// One row of an <see cref="AutoForm"/>: the caption label, the generated input and the (normally hidden)
    /// validation message under it, plus the conversions between the input's value type and the property type.
    /// </summary>
    internal sealed class FormField
    {
        private readonly AutoForm form;
        private string? error;

        internal FormField(AutoForm form, FormProperty property, string idPrefix)
        {
            this.form = form;
            Property = property;
            string id = idPrefix + "." + property.Name;

            Control = CreateControl(id);
            Control.Enabled = !property.ReadOnly;
            string? tooltip = property.Tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                Control.Tooltip = () => tooltip;
            }

            Caption = new Label(id + ".label", () => Property.Label)
            {
                VerticalAlign = UIAlign.Center
            };
            Error = new Label(id + ".error", () => error ?? string.Empty)
            {
                Font = UIFont.Small,
                Color = Color.Red,
                Visible = false,
                Wrap = true
            };
            Cell = new Stack(id + ".cell", horizontal: false, spacing: 4);
            Cell.Add(Control);
            Cell.Add(Error);
        }

        internal FormProperty Property { get; }

        /// <summary>The generated input.</summary>
        internal UIElement Control { get; }

        /// <summary>Caption in the first grid column.</summary>
        internal Label Caption { get; }

        /// <summary>Validation message (visible only while invalid).</summary>
        internal Label Error { get; }

        /// <summary>Second grid column: the control above the error label.</summary>
        internal Stack Cell { get; }

        /// <summary>True for inputs that run the validator through their own <c>Validate</c> hook before the setter (text / number).</summary>
        internal bool ValidatesInline => Property.Kind is FormFieldKind.Text or FormFieldKind.Number;

        // ---------------------------------------------------------------------------------------------------------
        //  Model access
        // ---------------------------------------------------------------------------------------------------------

        internal object? Read() => form.ReadModel(Control.Id, Property.Getter);

        internal void Write(object? value) => form.Guard(Control.Id, "set", () => Property.Setter(form.Model, value));

        /// <summary>Show (or with null: hide) the validation message.</summary>
        internal void ShowError(string? message)
        {
            error = message;
            Error.Visible = !string.IsNullOrEmpty(message);
        }

        /// <summary>Push the model value into inputs that keep their own edit state (number buffer, dropdown index).</summary>
        internal void SyncControl()
        {
            switch (Control)
            {
                case NumberInput number:
                    number.Value = ToDouble(Read());
                    break;
                case Dropdown dropdown:
                    dropdown.SelectedValue = ToText(Read());
                    break;
                default:
                    break; // reads through its getter every frame
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Conversions
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Convert an input value (bool / double / string) to the property type; null when it does not fit.</summary>
        internal object? FromControl(object controlValue)
        {
            try
            {
                return Property.Kind switch
                {
                    FormFieldKind.Bool => controlValue is bool b && b,
                    FormFieldKind.Number => ToNumber(controlValue is double d ? d : 0),
                    FormFieldKind.Enum => Enum.Parse(Property.Type, controlValue as string ?? string.Empty),
                    _ => controlValue as string ?? string.Empty
                };
            }
            catch (Exception ex) when (ex is ArgumentException or OverflowException or FormatException)
            {
                return null;
            }
        }

        private object ToNumber(double value)
        {
            if (Property.IsInteger)
            {
                value = Math.Round(value, MidpointRounding.AwayFromZero);
            }

            return Convert.ChangeType(value, Property.Type, CultureInfo.InvariantCulture);
        }

        private static double ToDouble(object? value) => value is IConvertible c ? c.ToDouble(CultureInfo.InvariantCulture) : 0;

        private static string ToText(object? value) => value?.ToString() ?? string.Empty;

        // ---------------------------------------------------------------------------------------------------------
        //  Control factory
        // ---------------------------------------------------------------------------------------------------------

        private UIElement CreateControl(string id)
        {
            return Property.Kind switch
            {
                FormFieldKind.Bool => new Checkbox(id, () => Read() is bool b && b, v => form.Commit(this, v)),
                FormFieldKind.Number => CreateNumberInput(id),
                FormFieldKind.Choice or FormFieldKind.Enum => CreateDropdown(id),
                _ => new TextInput(id, () => ToText(Read()), v => form.Commit(this, v))
                {
                    ValidateFunc = v => form.Validate(this, v)
                }
            };
        }

        private NumberInput CreateNumberInput(string id)
        {
            var input = new NumberInput(id, () => ToDouble(Read()), v => form.Commit(this, v), Property.Min, Property.Max, 1, clamp: true)
            {
                Decimals = Property.IsInteger ? 0 : 2,
                ValidateFunc = v => form.Validate(this, v)
            };
            return input;
        }

        private Dropdown CreateDropdown(string id)
        {
            string[] choices = Property.Choices;
            string[] labels = Property.Kind == FormFieldKind.Enum ? Array.ConvertAll(choices, FormReflection.SplitCamelCase) : choices;
            return new Dropdown(id, () => choices, () => labels, () => ToText(Read()), v => form.Commit(this, v));
        }
    }
}
