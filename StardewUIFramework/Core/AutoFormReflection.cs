using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace UIFramework.Core
{
    /// <summary>Which control a form generates for a property.</summary>
    internal enum FormFieldKind
    {
        /// <summary>Property type is not supported; the property is skipped.</summary>
        Unsupported,
        /// <summary><c>bool</c> → checkbox.</summary>
        Bool,
        /// <summary>Integer / floating-point / decimal → number input.</summary>
        Number,
        /// <summary><c>string</c> → text input.</summary>
        Text,
        /// <summary><c>string</c> with a <c>Choices</c> attribute → dropdown.</summary>
        Choice,
        /// <summary>Enum → dropdown of the enum names.</summary>
        Enum
    }

    /// <summary>
    /// Everything <see cref="AutoForm"/> needs to know about one model field: either a real property read once through
    /// reflection, or a descriptor-built field with its own getter / setter (no <see cref="Property"/>).
    /// </summary>
    internal sealed class FormProperty
    {
        internal FormProperty(PropertyInfo property, FormFieldKind kind)
        {
            Property = property;
            Kind = kind;
            Name = property.Name;
            Type = property.PropertyType;
            Getter = CompileGetter(property);
            Setter = property.SetValue;
            Label = FormReflection.SplitCamelCase(property.Name);
        }

        /// <summary>A compiled getter for an instance property (read every frame; no reflection or boxing of the call), else <see cref="PropertyInfo.GetValue(object)"/>.</summary>
        private static Func<object, object?> CompileGetter(PropertyInfo property)
        {
            MethodInfo? get = property.GetMethod;
            if (get == null || get.IsStatic || property.DeclaringType == null || property.GetIndexParameters().Length > 0)
            {
                return property.GetValue;
            }

            ParameterExpression target = Expression.Parameter(typeof(object), "model");
            Expression read = Expression.Property(Expression.Convert(target, property.DeclaringType), property);
            return Expression.Lambda<Func<object, object?>>(Expression.Convert(read, typeof(object)), target).Compile();
        }

        /// <summary>A field backed by accessors instead of a <see cref="PropertyInfo"/> (e.g. a data-defined form).</summary>
        internal FormProperty(string name, Type type, FormFieldKind kind, Func<object, object?> getter, Action<object, object?> setter)
        {
            Kind = kind;
            Name = name;
            Type = type;
            Getter = getter;
            Setter = setter;
            Label = FormReflection.SplitCamelCase(name);
        }

        /// <summary>The reflected property, or null for an accessor-backed field.</summary>
        internal PropertyInfo? Property { get; }
        internal FormFieldKind Kind { get; }
        internal string Name { get; }
        internal Type Type { get; }

        /// <summary>Reads the field's value from the model.</summary>
        internal Func<object, object?> Getter { get; }

        /// <summary>Writes the field's value into the model.</summary>
        internal Action<object, object?> Setter { get; }

        /// <summary>Validator taking (model, value) and returning an error message or null; checked before <see cref="Validator"/>.</summary>
        internal Func<object, object?, string?>? CustomValidator { get; set; }

        internal string Label { get; set; }
        internal string? Tooltip { get; set; }

        /// <summary>Title of the section this property starts, or null.</summary>
        internal string? Section { get; set; }

        internal bool ReadOnly { get; set; }

        /// <summary>Dropdown choices (<see cref="FormFieldKind.Choice"/> / <see cref="FormFieldKind.Enum"/>).</summary>
        internal string[] Choices { get; set; } = Array.Empty<string>();

        internal double Min { get; set; }
        internal double Max { get; set; }

        /// <summary>True for the integral numeric types (step 1, no decimals).</summary>
        internal bool IsInteger => FormReflection.IsIntegerType(Type);

        /// <summary><c>Validate&lt;Name&gt;</c> on the model, or null.</summary>
        internal MethodInfo? Validator { get; set; }
    }

    /// <summary>
    /// Reflection helpers for auto-forms. Attributes are matched by type name only, so consumers can declare their own
    /// <c>RangeAttribute</c>, <c>ChoicesAttribute</c>, <c>SectionAttribute</c>, <c>TooltipAttribute</c>,
    /// <c>DisplayNameAttribute</c>, <c>DisplayAttribute</c> and <c>ReadOnlyAttribute</c> (or use the BCL ones).
    /// </summary>
    internal static class FormReflection
    {
        private static readonly HashSet<Type> IntegerTypes = new() { typeof(int), typeof(uint), typeof(long) };
        private static readonly HashSet<Type> FloatTypes = new() { typeof(float), typeof(double), typeof(decimal) };

        internal static bool IsIntegerType(Type type) => IntegerTypes.Contains(type);

        /// <summary>The supported public read / write instance properties of <paramref name="model"/>, base class first, in declaration order.</summary>
        internal static FormProperty[] Describe(object model)
        {
            Type type = model.GetType();
            var result = new List<FormProperty>();
            foreach (PropertyInfo property in OrderedProperties(type))
            {
                FormFieldKind kind = KindOf(property);
                if (kind == FormFieldKind.Unsupported)
                {
                    UIServices.Log($"AddForm: property '{type.Name}.{property.Name}' of type {property.PropertyType.Name} is not supported and was skipped.", StardewModdingAPI.LogLevel.Debug);
                    continue;
                }

                result.Add(Build(type, property, kind));
            }

            return result.ToArray();
        }

        private static IEnumerable<PropertyInfo> OrderedProperties(Type type)
        {
            var properties = new List<PropertyInfo>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && property.GetSetMethod() != null)
                {
                    properties.Add(property);
                }
            }

            properties.Sort((a, b) =>
            {
                int depth = Depth(a.DeclaringType).CompareTo(Depth(b.DeclaringType));
                return depth != 0 ? depth : a.MetadataToken.CompareTo(b.MetadataToken);
            });
            return properties;
        }

        private static int Depth(Type? type)
        {
            int depth = 0;
            for (Type? t = type; t != null; t = t.BaseType)
            {
                depth++;
            }

            return depth;
        }

        private static FormFieldKind KindOf(PropertyInfo property)
        {
            Type type = property.PropertyType;
            if (type == typeof(bool))
            {
                return FormFieldKind.Bool;
            }

            if (IntegerTypes.Contains(type) || FloatTypes.Contains(type))
            {
                return FormFieldKind.Number;
            }

            if (type == typeof(string))
            {
                return FindAttribute(property, "Choices") != null ? FormFieldKind.Choice : FormFieldKind.Text;
            }

            return type.IsEnum ? FormFieldKind.Enum : FormFieldKind.Unsupported;
        }

        private static FormProperty Build(Type modelType, PropertyInfo property, FormFieldKind kind)
        {
            var result = new FormProperty(property, kind)
            {
                Validator = FindValidator(modelType, property),
                Tooltip = StringOf(FindAttribute(property, "Tooltip"), "Text", "Tooltip", "Description")
            };
            ApplyLabel(result, property);
            ApplySection(result, property);
            ApplyReadOnly(result, property);
            ApplyChoices(result, property);
            ApplyRange(result, property);
            return result;
        }

        private static void ApplyLabel(FormProperty result, PropertyInfo property)
        {
            Attribute? display = FindAttribute(property, "DisplayName") ?? FindAttribute(property, "Display");
            string? label = StringOf(display, "DisplayName", "Name");
            if (!string.IsNullOrEmpty(label))
            {
                result.Label = label;
            }

            result.Tooltip ??= StringOf(FindAttribute(property, "Display"), "Description");
        }

        private static void ApplySection(FormProperty result, PropertyInfo property)
        {
            Attribute? section = FindAttribute(property, "Section");
            if (section != null)
            {
                result.Section = StringOf(section, "Title", "Name", "Text") ?? string.Empty;
            }
        }

        private static void ApplyReadOnly(FormProperty result, PropertyInfo property)
        {
            Attribute? readOnly = FindAttribute(property, "ReadOnly");
            if (readOnly != null)
            {
                object? flag = readOnly.GetType().GetProperty("IsReadOnly")?.GetValue(readOnly);
                result.ReadOnly = flag is not bool b || b;
            }
        }

        private static void ApplyChoices(FormProperty result, PropertyInfo property)
        {
            if (result.Kind == FormFieldKind.Enum)
            {
                result.Choices = Enum.GetNames(property.PropertyType);
                return;
            }

            if (result.Kind != FormFieldKind.Choice)
            {
                return;
            }

            Attribute attribute = FindAttribute(property, "Choices")!;
            if (attribute.GetType().GetProperty("Values")?.GetValue(attribute) is string[] values)
            {
                result.Choices = values;
                return;
            }

            string csv = StringOf(attribute, "Csv", "Values") ?? string.Empty;
            result.Choices = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>Range from a <c>Range(min, max)</c> attribute (<c>Minimum</c> / <c>Maximum</c> members), else the type's limits.</summary>
        private static void ApplyRange(FormProperty result, PropertyInfo property)
        {
            if (result.Kind != FormFieldKind.Number)
            {
                return;
            }

            (result.Min, result.Max) = TypeLimits(property.PropertyType);
            Attribute? range = FindAttribute(property, "Range");
            if (range == null)
            {
                return;
            }

            double? min = NumberOf(range, "Minimum", "Min");
            double? max = NumberOf(range, "Maximum", "Max");
            if (min.HasValue && max.HasValue)
            {
                result.Min = Math.Min(min.Value, max.Value);
                result.Max = Math.Max(min.Value, max.Value);
            }
        }

        private static (double min, double max) TypeLimits(Type type)
        {
            if (type == typeof(int))
            {
                return (int.MinValue, int.MaxValue);
            }

            if (type == typeof(uint))
            {
                return (uint.MinValue, uint.MaxValue);
            }

            if (type == typeof(long))
            {
                return (long.MinValue, long.MaxValue);
            }

            if (type == typeof(float))
            {
                return (float.MinValue, float.MaxValue);
            }

            if (type == typeof(decimal))
            {
                return ((double)decimal.MinValue, (double)decimal.MaxValue);
            }

            return (double.MinValue, double.MaxValue);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Attribute lookup by name
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>First attribute on <paramref name="member"/> whose type is named <paramref name="name"/> or <paramref name="name"/>Attribute.</summary>
        private static Attribute? FindAttribute(MemberInfo member, string name)
        {
            foreach (Attribute attribute in member.GetCustomAttributes(inherit: true))
            {
                string typeName = attribute.GetType().Name;
                if (typeName == name || typeName == name + "Attribute")
                {
                    return attribute;
                }
            }

            return null;
        }

        /// <summary>A string member of <paramref name="attribute"/>: the first of <paramref name="preferred"/> that exists, else its first public string property.</summary>
        private static string? StringOf(Attribute? attribute, params string[] preferred)
        {
            if (attribute == null)
            {
                return null;
            }

            Type type = attribute.GetType();
            foreach (string name in preferred)
            {
                if (type.GetProperty(name)?.GetValue(attribute) is string preferredValue)
                {
                    return preferredValue;
                }
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0 && property.GetValue(attribute) is string value)
                {
                    return value;
                }
            }

            return null;
        }

        private static double? NumberOf(Attribute attribute, params string[] names)
        {
            foreach (string name in names)
            {
                object? value = attribute.GetType().GetProperty(name)?.GetValue(attribute);
                if (value is IConvertible convertible && value is not string)
                {
                    return convertible.ToDouble(CultureInfo.InvariantCulture);
                }

                if (value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Validation
        // ---------------------------------------------------------------------------------------------------------

        /// <summary><c>Validate&lt;Property&gt;()</c> or <c>Validate&lt;Property&gt;(value)</c> on the model, public or not.</summary>
        private static MethodInfo? FindValidator(Type modelType, PropertyInfo property)
        {
            string name = "Validate" + property.Name;
            MethodInfo? parameterless = null;
            foreach (MethodInfo method in modelType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != name || method.IsGenericMethod)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(property.PropertyType))
                {
                    return method;
                }

                if (parameters.Length == 0)
                {
                    parameterless = method;
                }
            }

            return parameterless;
        }

        /// <summary>
        /// Run the property's validator against <paramref name="value"/>. A parameterless validator sees the value
        /// through the property (which is restored afterwards). Returns the error message, or null when valid.
        /// </summary>
        internal static string? Validate(object model, FormProperty property, object? value, string genericMessage)
        {
            if (property.CustomValidator != null)
            {
                string? custom = property.CustomValidator(model, value);
                return string.IsNullOrEmpty(custom) ? null : custom;
            }

            MethodInfo? validator = property.Validator;
            if (validator == null)
            {
                return null;
            }

            object? result;
            if (validator.GetParameters().Length == 1)
            {
                result = validator.Invoke(model, new[] { value });
            }
            else
            {
                object? previous = property.Getter(model);
                property.Setter(model, value);
                try
                {
                    result = validator.Invoke(model, null);
                }
                finally
                {
                    property.Setter(model, previous);
                }
            }

            return result switch
            {
                bool ok => ok ? null : genericMessage,
                string message => string.IsNullOrEmpty(message) ? null : message,
                _ => null
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Naming
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>"FarmName" → "Farm Name", "UIScale" → "UI Scale", "Day2" → "Day 2".</summary>
        internal static string SplitCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && NeedsSpaceBefore(name, i))
                {
                    sb.Append(' ');
                }

                sb.Append(c == '_' ? ' ' : c);
            }

            return sb.ToString();
        }

        private static bool NeedsSpaceBefore(string name, int i)
        {
            char c = name[i];
            char prev = name[i - 1];
            if (prev == '_' || c == '_')
            {
                return false;
            }

            if (char.IsUpper(c))
            {
                bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                return char.IsLower(prev) || char.IsDigit(prev) || (char.IsUpper(prev) && nextLower);
            }

            return char.IsDigit(c) && char.IsLetter(prev);
        }
    }
}
