using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using StardewValley;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// Duck-typed access to plain C# objects exposed to data (<c>ExposeModel</c>, <c>ExposeRows</c>): members are the
    /// public instance properties (without index parameters) and fields, matched case-insensitively and cached per
    /// type. Writes convert like <see cref="FormField"/> (integers round half away from zero, enums by name or number).
    /// An object implementing <see cref="INotifyPropertyChanged"/> / <see cref="INotifyCollectionChanged"/> is watched
    /// the first time it is read: its notifications bump the state epoch, so its reads are cached like state reads;
    /// reads of other objects are volatile (cached per tick).
    /// </summary>
    internal static class ModelAccessor
    {
        private static readonly Dictionary<(Type Type, string Name), MemberInfo?> Members = new();
        private static readonly ConditionalWeakTable<object, object> Watched = new();
        private static readonly PropertyChangedEventHandler OnPropertyChanged = (_, _) => Changed();
        private static readonly NotifyCollectionChangedEventHandler OnCollectionChanged = (_, _) => Changed();

        private static void Changed() => State.DataStateStore.Active?.BumpGlobal();

        /// <summary>
        /// Watch <paramref name="target"/> for change notifications (once); true when it notifies (its reads need not be
        /// volatile). Safe to call on every read.
        /// </summary>
        internal static bool Watch(object target)
        {
            bool notifies = target is INotifyPropertyChanged || target is INotifyCollectionChanged;
            if (!notifies || State.DataStateStore.Active == null)
            {
                return false;
            }

            if (!Watched.TryGetValue(target, out _))
            {
                Watched.Add(target, target);
                if (target is INotifyPropertyChanged property)
                {
                    property.PropertyChanged += OnPropertyChanged;
                }

                if (target is INotifyCollectionChanged collection)
                {
                    collection.CollectionChanged += OnCollectionChanged;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Values
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A CLR value as a data value (see <see cref="DataValue.FromObject"/>; plain objects stay opaque, collections become lists).</summary>
        internal static DataValue ToValue(object? value)
        {
            if (value is INotifyCollectionChanged)
            {
                Watch(value);
            }

            return DataValue.FromObject(value);
        }

        /// <summary>A data value as a plain CLR value for C# code: null, bool, double, string, object[], a string-keyed dictionary for rows, or the wrapped object.</summary>
        internal static object? ToClr(DataValue value)
        {
            switch (value.Kind)
            {
                case DataKind.Null:
                    return null;
                case DataKind.List:
                {
                    IReadOnlyList<DataValue> list = value.AsList();
                    var result = new object?[list.Count];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ToClr(list[i]);
                    }

                    return result;
                }

                case DataKind.Object:
                    switch (value.AsObject())
                    {
                        case RowFields fields:
                        {
                            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                            foreach ((string key, DataValue field) in fields)
                            {
                                result[key] = ToClr(field);
                            }

                            return result;
                        }

                        case DataSourceHandle.SourceRow row:
                            return row.Index;
                        default:
                            return value.AsObject();
                    }

                default:
                    return value.AsObject();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Reading
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Read member <paramref name="name"/> of <paramref name="target"/>; false when it has no such public member.</summary>
        internal static bool TryGetMember(object target, string name, out DataValue value, out bool isVolatile)
        {
            value = DataValue.Null;
            isVolatile = !Watch(target);
            if (!TryGetRaw(target, name, out object? raw))
            {
                return false;
            }

            value = ToValue(raw);
            return true;
        }

        /// <summary>The raw CLR value of a member (dictionaries by key).</summary>
        internal static bool TryGetRaw(object target, string name, out object? raw)
        {
            raw = null;
            if (target is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        raw = entry.Value;
                        return true;
                    }
                }

                return false;
            }

            MemberInfo? member = Find(target.GetType(), name);
            try
            {
                switch (member)
                {
                    case PropertyInfo property:
                        raw = property.GetValue(target);
                        return true;
                    case FieldInfo field:
                        raw = field.GetValue(target);
                        return true;
                    default:
                        return false;
                }
            }
            catch (TargetInvocationException ex)
            {
                UIServices.Log($"Reading '{name}' of {target.GetType().Name} failed: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
        }

        /// <summary>The item a row object stands for (its <c>Item</c> member), or null.</summary>
        internal static Item? ItemOf(object target)
        {
            return TryGetRaw(target, "item", out object? raw) && raw is Item item ? item : null;
        }

        /// <summary>The public readable property (no index parameters) or field named <paramref name="name"/> (exact case first).</summary>
        private static MemberInfo? Find(Type type, string name)
        {
            var key = (type, name);
            if (Members.TryGetValue(key, out MemberInfo? cached))
            {
                return cached;
            }

            MemberInfo? found = null;
            foreach (bool ignoreCase in new[] { false, true })
            {
                StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.CanRead && property.GetIndexParameters().Length == 0 && string.Equals(property.Name, name, comparison))
                    {
                        found = property;
                        break;
                    }
                }

                if (found == null)
                {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (string.Equals(field.Name, name, comparison))
                        {
                            found = field;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    break;
                }
            }

            Members[key] = found;
            return found;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Writing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Write <paramref name="value"/> at <paramref name="path"/> (member names) under <paramref name="root"/>, converting
        /// it to the member's type; false (with an error) when a step is missing, read-only or the value does not fit.
        /// </summary>
        internal static bool TrySetPath(object root, IReadOnlyList<string> path, DataValue value, out string error)
        {
            error = string.Empty;
            if (path.Count == 0)
            {
                error = "a model itself cannot be replaced; bind to one of its members.";
                return false;
            }

            object target = root;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (!TryGetRaw(target, path[i], out object? next) || next == null)
                {
                    error = $"'{path[i]}' is missing or null.";
                    return false;
                }

                target = next;
            }

            string name = path[^1];
            if (target is IDictionary dictionary)
            {
                dictionary[name] = ToClr(value);
                Changed();
                return true;
            }

            if (target.GetType().IsValueType)
            {
                error = $"'{(path.Count >= 2 ? path[^2] : "the model")}' is a struct; bind to a member of a class instead.";
                return false;
            }

            MemberInfo? member = Find(target.GetType(), name);
            Type? type = member switch
            {
                PropertyInfo { CanWrite: true } property when property.SetMethod?.IsPublic == true => property.PropertyType,
                FieldInfo { IsInitOnly: false, IsLiteral: false } field => field.FieldType,
                _ => null
            };
            if (type == null)
            {
                error = member == null ? $"{target.GetType().Name} has no public member '{name}'." : $"'{name}' of {target.GetType().Name} is read-only.";
                return false;
            }

            if (!TryConvert(value, type, out object? converted))
            {
                error = $"'{value.AsString()}' does not fit '{name}' ({type.Name}).";
                return false;
            }

            try
            {
                if (member is PropertyInfo property)
                {
                    property.SetValue(target, converted);
                }
                else
                {
                    ((FieldInfo)member!).SetValue(target, converted);
                }
            }
            catch (TargetInvocationException ex)
            {
                error = $"setting '{name}' failed: {ex.InnerException?.Message ?? ex.Message}";
                return false;
            }

            Changed();
            return true;
        }

        /// <summary>Convert a data value to <paramref name="type"/> (the conversions of <see cref="FormField"/>).</summary>
        internal static bool TryConvert(DataValue value, Type type, out object? result)
        {
            result = null;
            Type? underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                if (value.IsNull)
                {
                    return true;
                }

                type = underlying;
            }

            try
            {
                if (type == typeof(string))
                {
                    result = value.AsString();
                    return true;
                }

                if (type == typeof(bool))
                {
                    result = value.AsBool();
                    return true;
                }

                if (type.IsEnum)
                {
                    if (value.Kind == DataKind.Number)
                    {
                        result = Enum.ToObject(type, (long)Math.Round(value.AsNumber()));
                        return true;
                    }

                    return Enum.TryParse(type, value.AsString().Trim(), ignoreCase: true, out result);
                }

                if (FormReflection.IsIntegerType(type))
                {
                    result = Convert.ChangeType(Math.Round(value.AsNumber(), MidpointRounding.AwayFromZero), type, CultureInfo.InvariantCulture);
                    return true;
                }

                if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                {
                    result = Convert.ChangeType(value.AsNumber(), type, CultureInfo.InvariantCulture);
                    return true;
                }

                object? raw = ToClr(value);
                if (raw == null ? !type.IsValueType : type.IsInstanceOfType(raw))
                {
                    result = raw;
                    return true;
                }
            }
            catch (Exception ex) when (ex is OverflowException or InvalidCastException or FormatException or ArgumentException)
            {
                return false;
            }

            return false;
        }
    }
}
