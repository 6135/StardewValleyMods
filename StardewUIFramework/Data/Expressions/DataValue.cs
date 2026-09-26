using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UIFramework.Core;

namespace UIFramework.Data.Expressions
{
    /// <summary>The runtime type of a <see cref="DataValue"/>.</summary>
    internal enum DataKind : byte
    {
        Null,
        Bool,
        Number,
        String,
        List,
        Object
    }

    /// <summary>
    /// An immutable tagged value produced and consumed by the expression engine: null, bool, number (double), string,
    /// list of values, or an opaque object (items, models) whose members only a scope can read.
    /// </summary>
    /// <remarks>
    /// Conversions are invariant-culture and mirror <see cref="ReactiveValue"/> so values round-trip through state
    /// signals unchanged:
    /// <list type="bullet">
    ///   <item>text → number: invariant float parse; otherwise <c>"true"</c> → 1, anything else → 0.</item>
    ///   <item>text → flag: numeric text is true when non-zero; otherwise a case-insensitive <c>"true"</c>; anything else is false.</item>
    ///   <item>number → text: round-trip (<c>"R"</c>) invariant format; flag → text: <c>"True"</c> / <c>"False"</c>.</item>
    /// </list>
    /// </remarks>
    internal readonly struct DataValue : IEquatable<DataValue>
    {
        /// <summary>The null value (also the <c>default</c> of the struct).</summary>
        public static readonly DataValue Null = default;

        /// <summary>The boolean true value.</summary>
        public static readonly DataValue True = new(DataKind.Bool, 1, null);

        /// <summary>The boolean false value.</summary>
        public static readonly DataValue False = new(DataKind.Bool, 0, null);

        /// <summary>The number zero.</summary>
        public static readonly DataValue Zero = new(DataKind.Number, 0, null);

        /// <summary>The empty string.</summary>
        public static readonly DataValue EmptyString = new(DataKind.String, 0, string.Empty);

        /// <summary>An empty list.</summary>
        public static readonly DataValue EmptyList = new(DataKind.List, 0, Array.Empty<DataValue>());

        /// <summary>String, list or opaque object payload.</summary>
        private readonly object? reference;

        /// <summary>Number payload (also 0/1 for booleans).</summary>
        private readonly double number;

        private DataValue(DataKind kind, double number, object? reference)
        {
            Kind = kind;
            this.number = number;
            this.reference = reference;
        }

        /// <summary>The runtime type of this value.</summary>
        public DataKind Kind { get; }

        /// <summary>True for the null value.</summary>
        public bool IsNull => Kind == DataKind.Null;

        // -------------------------------------------------------------------------------------------------------------
        //  Construction
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>A boolean value.</summary>
        public static DataValue FromBool(bool value) => value ? True : False;

        /// <summary>A number value; NaN and infinities become 0 so they never reach the UI.</summary>
        public static DataValue FromNumber(double value) => new(DataKind.Number, double.IsFinite(value) ? value : 0, null);

        /// <summary>A string value (null becomes the null value).</summary>
        public static DataValue FromString(string? value) => value == null ? Null : new DataValue(DataKind.String, 0, value);

        /// <summary>A list value (null becomes the null value). The list is not copied; callers must not mutate it afterwards.</summary>
        public static DataValue FromList(IReadOnlyList<DataValue>? values) => values == null ? Null : new DataValue(DataKind.List, 0, values);

        /// <summary>Wrap <paramref name="value"/> as an opaque object without inspecting it (null becomes the null value).</summary>
        public static DataValue Opaque(object? value) => value == null ? Null : new DataValue(DataKind.Object, 0, value);

        /// <summary>Convert a stored signal value, keeping the kind it was last written as.</summary>
        public static DataValue FromReactive(in ReactiveValue value)
        {
            return value.Kind switch
            {
                ReactiveKind.Number => FromNumber(value.Number),
                ReactiveKind.Flag => FromBool(value.Flag),
                _ => FromString(value.Text)
            };
        }

        /// <summary>
        /// Map a CLR value: primitives, strings, enums (by name), <see cref="ReactiveValue"/>, and sequences (as lists,
        /// capped at <see cref="ExpressionLimits.MaxListLength"/>). Dictionaries and everything else stay opaque.
        /// </summary>
        public static DataValue FromObject(object? value)
        {
            switch (value)
            {
                case null:
                    return Null;
                case DataValue data:
                    return data;
                case bool flag:
                    return FromBool(flag);
                case string text:
                    return FromString(text);
                case char c:
                    return FromString(c.ToString());
                case Enum e:
                    return FromString(e.ToString());
                case ReactiveValue reactive:
                    return FromReactive(reactive);
                case double d:
                    return FromNumber(d);
                case float f:
                    return FromNumber(f);
                case int i:
                    return FromNumber(i);
                case long l:
                    return FromNumber(l);
                case IConvertible convertible when IsNumeric(convertible.GetTypeCode()):
                    return FromNumber(convertible.ToDouble(CultureInfo.InvariantCulture));
                case IReadOnlyList<DataValue> list:
                    return FromList(list);
                case IDictionary:
                case IReadOnlyDictionary<string, DataValue>:
                case IReadOnlyDictionary<string, object?>:
                case IDictionary<string, object?>:
                    return Opaque(value);
                case IEnumerable sequence:
                    return FromList(ToList(sequence));
                default:
                    return Opaque(value);
            }
        }

        private static bool IsNumeric(TypeCode code)
        {
            return code is TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32
                or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
        }

        private static List<DataValue> ToList(IEnumerable sequence)
        {
            List<DataValue> list = new();
            foreach (object? item in sequence)
            {
                if (list.Count >= ExpressionLimits.MaxListLength)
                {
                    break;
                }

                list.Add(FromObject(item));
            }

            return list;
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Conversion
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>The value as a flag (see the conversion rules on the type).</summary>
        public bool AsBool()
        {
            switch (Kind)
            {
                case DataKind.Bool:
                    return number != 0;
                case DataKind.Number:
                    return !Numbers.Same(number, 0);
                case DataKind.String:
                    return TextToBool((string)reference!);
                case DataKind.List:
                    return ((IReadOnlyList<DataValue>)reference!).Count > 0;
                case DataKind.Object:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The value as a number (see the conversion rules on the type).</summary>
        public double AsNumber()
        {
            switch (Kind)
            {
                case DataKind.Bool:
                case DataKind.Number:
                    return number;
                case DataKind.String:
                    return TextToNumber((string)reference!);
                default:
                    return 0;
            }
        }

        /// <summary>The value as text: null is empty, lists join with <c>", "</c>, opaque objects use <see cref="object.ToString"/>.</summary>
        public string AsString()
        {
            switch (Kind)
            {
                case DataKind.Bool:
                    return number != 0 ? bool.TrueString : bool.FalseString;
                case DataKind.Number:
                    return NumberToText(number);
                case DataKind.String:
                    return (string)reference!;
                case DataKind.List:
                    return JoinList((IReadOnlyList<DataValue>)reference!, ", ");
                case DataKind.Object:
                    return reference!.ToString() ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>The value as a list: lists as-is, null as empty, anything else as a one-element list.</summary>
        public IReadOnlyList<DataValue> AsList()
        {
            return Kind switch
            {
                DataKind.List => (IReadOnlyList<DataValue>)reference!,
                DataKind.Null => Array.Empty<DataValue>(),
                _ => new[] { this }
            };
        }

        /// <summary>The CLR payload: null, bool, double, string, <see cref="IReadOnlyList{DataValue}"/> or the opaque object.</summary>
        public object? AsObject()
        {
            return Kind switch
            {
                DataKind.Bool => number != 0,
                DataKind.Number => number,
                _ => reference
            };
        }

        /// <summary>Convert to a signal value (lists and objects are stored as their text).</summary>
        public ReactiveValue ToReactive()
        {
            return Kind switch
            {
                DataKind.Bool => ReactiveValue.FromFlag(number != 0),
                DataKind.Number => ReactiveValue.FromNumber(number),
                _ => ReactiveValue.FromText(AsString())
            };
        }

        /// <summary>Text → number (invariant parse, then <c>"true"</c> → 1, else 0).</summary>
        internal static double TextToNumber(string text)
        {
            if (TryParseNumber(text, out double value))
            {
                return value;
            }

            return string.Equals(text.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        /// <summary>Text → flag (numeric text non-zero, else case-insensitive <c>"true"</c>).</summary>
        internal static bool TextToBool(string text)
        {
            if (TryParseNumber(text, out double value))
            {
                return !Numbers.Same(value, 0);
            }

            return string.Equals(text.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Invariant float parse that rejects NaN / infinity.</summary>
        internal static bool TryParseNumber(string text, out double value)
        {
            if (text.Length > 0 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Round-trip invariant text for a number (negative zero prints as <c>0</c>).</summary>
        internal static string NumberToText(double value)
        {
            if (value == 0)
            {
                return "0";
            }

            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>Join the text of every element with <paramref name="separator"/>.</summary>
        internal static string JoinList(IReadOnlyList<DataValue> list, string separator)
        {
            if (list.Count == 0)
            {
                return string.Empty;
            }

            if (list.Count == 1)
            {
                return list[0].AsString();
            }

            StringBuilder builder = new();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(separator);
                }

                builder.Append(list[i].AsString());
                if (builder.Length > ExpressionLimits.MaxStringLength)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Members
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Structural member access that needs no scope: list index / <c>length</c> / <c>count</c>, string
        /// <c>length</c> / character index, and string-keyed dictionaries wrapped as opaque objects.
        /// </summary>
        internal bool TryGetMember(string key, out DataValue value)
        {
            switch (Kind)
            {
                case DataKind.List:
                {
                    IReadOnlyList<DataValue> list = (IReadOnlyList<DataValue>)reference!;
                    if (TryIndex(key, list.Count, out int index))
                    {
                        value = list[index];
                        return true;
                    }

                    if (IsLengthKey(key))
                    {
                        value = FromNumber(list.Count);
                        return true;
                    }

                    break;
                }

                case DataKind.String:
                {
                    string text = (string)reference!;
                    if (TryIndex(key, text.Length, out int index))
                    {
                        value = FromString(text[index].ToString());
                        return true;
                    }

                    if (IsLengthKey(key))
                    {
                        value = FromNumber(text.Length);
                        return true;
                    }

                    break;
                }

                case DataKind.Object:
                    switch (reference)
                    {
                        case IReadOnlyDictionary<string, DataValue> data when data.TryGetValue(key, out DataValue found):
                            value = found;
                            return true;
                        case IReadOnlyDictionary<string, object?> ro when ro.TryGetValue(key, out object? found):
                            value = FromObject(found);
                            return true;
                        case IDictionary<string, object?> rw when rw.TryGetValue(key, out object? found):
                            value = FromObject(found);
                            return true;
                        case IDictionary legacy when legacy.Contains(key):
                            value = FromObject(legacy[key]);
                            return true;
                    }

                    break;
            }

            value = Null;
            return false;
        }

        private static bool IsLengthKey(string key)
        {
            return string.Equals(key, "length", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "count", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse <paramref name="key"/> as an in-range index (negative counts from the end).</summary>
        private static bool TryIndex(string key, int count, out int index)
        {
            if (int.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out index))
            {
                if (index < 0)
                {
                    index += count;
                }

                return index >= 0 && index < count;
            }

            return false;
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Equality and ordering
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Expression equality (<c>==</c>): null only equals null; a number compares numerically with anything that
        /// parses as one; a bool compares with the other side's flag; strings compare ordinally; lists element-wise;
        /// objects by <see cref="object.Equals(object?)"/>.
        /// </summary>
        public static bool LooseEquals(in DataValue left, in DataValue right)
        {
            if (left.Kind == DataKind.Null || right.Kind == DataKind.Null)
            {
                return left.Kind == right.Kind;
            }

            if (left.Kind == DataKind.Bool || right.Kind == DataKind.Bool)
            {
                return left.AsBool() == right.AsBool();
            }

            if (left.Kind == DataKind.Number || right.Kind == DataKind.Number)
            {
                DataValue other = left.Kind == DataKind.Number ? right : left;
                if (other.Kind == DataKind.Number || (other.Kind == DataKind.String && TryParseNumber((string)other.reference!, out _)))
                {
                    return Numbers.Same(left.AsNumber(), right.AsNumber());
                }

                return string.Equals(left.AsString(), right.AsString(), StringComparison.Ordinal);
            }

            if (left.Kind == DataKind.List && right.Kind == DataKind.List)
            {
                IReadOnlyList<DataValue> a = (IReadOnlyList<DataValue>)left.reference!;
                IReadOnlyList<DataValue> b = (IReadOnlyList<DataValue>)right.reference!;
                if (a.Count != b.Count)
                {
                    return false;
                }

                for (int i = 0; i < a.Count; i++)
                {
                    if (!LooseEquals(a[i], b[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (left.Kind == DataKind.Object && right.Kind == DataKind.Object)
            {
                return Equals(left.reference, right.reference);
            }

            return string.Equals(left.AsString(), right.AsString(), StringComparison.Ordinal);
        }

        /// <summary>
        /// Ordering for <c>&lt; &lt;= &gt; &gt;=</c>: two strings that are not both numeric compare ordinally,
        /// everything else numerically.
        /// </summary>
        public static int Compare(in DataValue left, in DataValue right)
        {
            if (left.Kind == DataKind.String && right.Kind == DataKind.String)
            {
                string a = (string)left.reference!;
                string b = (string)right.reference!;
                if (!(TryParseNumber(a, out double x) && TryParseNumber(b, out double y)))
                {
                    return Math.Sign(string.CompareOrdinal(a, b));
                }

                return Numbers.Same(x, y) ? 0 : x.CompareTo(y);
            }

            double l = left.AsNumber();
            double r = right.AsNumber();
            return Numbers.Same(l, r) ? 0 : l.CompareTo(r);
        }

        /// <summary>Strict equality: same kind and same payload.</summary>
        public bool Equals(DataValue other)
        {
            if (Kind != other.Kind)
            {
                return false;
            }

            return Kind switch
            {
                DataKind.Null => true,
                DataKind.Bool or DataKind.Number => number.Equals(other.number),
                DataKind.String => string.Equals((string)reference!, (string)other.reference!, StringComparison.Ordinal),
                _ => ReferenceEquals(reference, other.reference) || Equals(reference, other.reference)
            };
        }

        public override bool Equals(object? obj) => obj is DataValue other && Equals(other);

        public override int GetHashCode()
        {
            return Kind switch
            {
                DataKind.Null => 0,
                DataKind.Bool or DataKind.Number => HashCode.Combine(Kind, number),
                _ => HashCode.Combine(Kind, reference)
            };
        }

        public static bool operator ==(DataValue left, DataValue right) => left.Equals(right);

        public static bool operator !=(DataValue left, DataValue right) => !left.Equals(right);

        /// <summary>Same as <see cref="AsString"/>.</summary>
        public override string ToString() => AsString();
    }
}
