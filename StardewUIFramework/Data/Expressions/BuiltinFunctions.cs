using System;
using System.Collections.Generic;
using System.Globalization;

namespace UIFramework.Data.Expressions
{
    /// <summary>
    /// The pure built-in functions (no game access). Game and UI functions (<c>gsq token loc isOpen focused hovered
    /// bounds itemName</c>) are registered by the data layer into <see cref="FunctionRegistry.Default"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>math: <c>round(n[, digits]) floor(n) ceil(n) abs(n) min(a, b, ...) max(a, b, ...) clamp(n, lo, hi)</c>; <c>min</c>/<c>max</c> with one list argument use its elements.</item>
    ///   <item>formatting: <c>format(n, fmt)</c> (.NET invariant numeric format), <c>money(n)</c> → <c>1,234g</c>, <c>percent(n[, digits])</c> → <c>25%</c> for 0.25.</item>
    ///   <item>strings: <c>len upper lower trim contains(hay, needle[, ignoreCase]) replace(s, old, new) substring(s, start[, length]) join(list[, sep]) quote(s)</c>.</item>
    ///   <item>conversion: <c>num str bool</c>.</item>
    /// </list>
    /// </remarks>
    internal static class BuiltinFunctions
    {
        /// <summary>Longest accepted <c>format()</c> pattern.</summary>
        private const int MaxFormatLength = 64;
        private const int MaxFormatPrecision = 30;

        /// <summary>Register every built-in into <paramref name="registry"/>.</summary>
        internal static void RegisterAll(FunctionRegistry registry)
        {
            // math
            registry.Register("round", 1, 2, Round);
            registry.Register("floor", 1, 1, (_, a) => DataValue.FromNumber(Math.Floor(a[0].AsNumber())));
            registry.Register("ceil", 1, 1, (_, a) => DataValue.FromNumber(Math.Ceiling(a[0].AsNumber())));
            registry.Register("abs", 1, 1, (_, a) => DataValue.FromNumber(Math.Abs(a[0].AsNumber())));
            registry.Register("min", 1, -1, (c, a) => Extreme(c, a, pickMax: false));
            registry.Register("max", 1, -1, (c, a) => Extreme(c, a, pickMax: true));
            registry.Register("clamp", 3, 3, (_, a) => DataValue.FromNumber(Math.Min(Math.Max(a[0].AsNumber(), a[1].AsNumber()), a[2].AsNumber())));

            // formatting
            registry.Register("format", 2, 2, Format);
            registry.Register("money", 1, 1, (_, a) => DataValue.FromString(Money(a[0].AsNumber())));
            registry.Register("percent", 1, 2, Percent);

            // strings
            registry.Register("len", 1, 1, (_, a) => DataValue.FromNumber(Length(a[0])));
            registry.Register("upper", 1, 1, (_, a) => DataValue.FromString(a[0].AsString().ToUpperInvariant()));
            registry.Register("lower", 1, 1, (_, a) => DataValue.FromString(a[0].AsString().ToLowerInvariant()));
            registry.Register("trim", 1, 1, (_, a) => DataValue.FromString(a[0].AsString().Trim()));
            registry.Register("contains", 2, 3, Contains);
            registry.Register("replace", 3, 3, Replace);
            registry.Register("substring", 2, 3, Substring);
            registry.Register("join", 1, 2, Join);
            registry.Register("quote", 1, 1, (c, a) => c.Text(Quote(a[0].AsString())));

            // conversion
            registry.Register("num", 1, 1, (_, a) => DataValue.FromNumber(a[0].AsNumber()));
            registry.Register("str", 1, 1, (c, a) => c.Text(a[0].AsString()));
            registry.Register("bool", 1, 1, (_, a) => DataValue.FromBool(a[0].AsBool()));
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Math
        // -------------------------------------------------------------------------------------------------------------

        private static DataValue Round(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            int digits = args.Length > 1 ? (int)Math.Clamp(args[1].AsNumber(), 0, 15) : 0;
            return DataValue.FromNumber(Math.Round(args[0].AsNumber(), digits, MidpointRounding.AwayFromZero));
        }

        private static DataValue Extreme(ExpressionContext context, ReadOnlySpan<DataValue> args, bool pickMax)
        {
            if (args.Length == 1 && args[0].Kind == DataKind.List)
            {
                IReadOnlyList<DataValue> list = args[0].AsList();
                if (list.Count == 0)
                {
                    return DataValue.Null;
                }

                context.Consume(list.Count);
                double best = list[0].AsNumber();
                for (int i = 1; i < list.Count; i++)
                {
                    double value = list[i].AsNumber();
                    best = pickMax ? Math.Max(best, value) : Math.Min(best, value);
                }

                return DataValue.FromNumber(best);
            }

            double result = args[0].AsNumber();
            for (int i = 1; i < args.Length; i++)
            {
                double value = args[i].AsNumber();
                result = pickMax ? Math.Max(result, value) : Math.Min(result, value);
            }

            return DataValue.FromNumber(result);
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Formatting
        // -------------------------------------------------------------------------------------------------------------

        private static DataValue Format(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            string pattern = args[1].AsString();
            if (pattern.Length > MaxFormatLength)
            {
                context.Fail($"format() pattern is longer than {MaxFormatLength} characters.");
            }

            // a huge precision would build a huge string before the length check could reject it
            if (PrecisionTooLarge(pattern))
            {
                context.Fail($"format(): the precision of '{pattern}' is above {MaxFormatPrecision}.");
            }

            try
            {
                return context.Text(args[0].AsNumber().ToString(pattern, CultureInfo.InvariantCulture));
            }
            catch (FormatException)
            {
                context.Fail($"format(): '{pattern}' is not a valid number format.");
                return DataValue.Null;
            }
        }

        /// <summary>True for a standard format (one letter plus digits, <c>"F2"</c>) whose precision is above <see cref="MaxFormatPrecision"/>.</summary>
        private static bool PrecisionTooLarge(string pattern)
        {
            if (pattern.Length < 2 || !((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')))
            {
                return false;
            }

            int precision = 0;
            for (int i = 1; i < pattern.Length; i++)
            {
                char c = pattern[i];
                if (c < '0' || c > '9')
                {
                    return false; // a custom format
                }

                precision = Math.Min(precision * 10 + (c - '0'), MaxFormatPrecision + 1);
            }

            return precision > MaxFormatPrecision;
        }

        /// <summary>Money text in English form whatever the game language: rounded, invariant thousands separators, <c>g</c> suffix (<c>1,234g</c>).</summary>
        internal static string Money(double amount)
        {
            double rounded = Math.Round(Math.Clamp(amount, -1e15, 1e15), MidpointRounding.AwayFromZero);
            return ((long)rounded).ToString("#,0", CultureInfo.InvariantCulture) + "g";
        }

        private static DataValue Percent(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            int digits = args.Length > 1 ? (int)Math.Clamp(args[1].AsNumber(), 0, 10) : 0;
            double value = Math.Round(args[0].AsNumber() * 100, digits, MidpointRounding.AwayFromZero);
            if (value == 0)
            {
                value = 0; // drop negative zero
            }

            return DataValue.FromString(value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%");
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Strings
        // -------------------------------------------------------------------------------------------------------------

        private static int Length(in DataValue value)
        {
            return value.Kind switch
            {
                DataKind.Null => 0,
                DataKind.List => value.AsList().Count,
                _ => value.AsString().Length
            };
        }

        private static DataValue Contains(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            bool ignoreCase = args.Length > 2 && args[2].AsBool();
            if (args[0].Kind == DataKind.List)
            {
                IReadOnlyList<DataValue> list = args[0].AsList();
                context.Consume(list.Count);
                foreach (DataValue item in list)
                {
                    if (ignoreCase
                        ? string.Equals(item.AsString(), args[1].AsString(), StringComparison.OrdinalIgnoreCase)
                        : DataValue.LooseEquals(item, args[1]))
                    {
                        return DataValue.True;
                    }
                }

                return DataValue.False;
            }

            StringComparison comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return DataValue.FromBool(args[0].AsString().Contains(args[1].AsString(), comparison));
        }

        private static DataValue Replace(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            string text = args[0].AsString();
            string search = args[1].AsString();
            string replacement = args[2].AsString();
            if (search.Length == 0 || text.Length == 0)
            {
                return DataValue.FromString(text);
            }

            // check the final length before building it, so a short pattern with a long replacement can't allocate huge strings
            if (replacement.Length > search.Length)
            {
                long occurrences = 0;
                for (int i = text.IndexOf(search, StringComparison.Ordinal); i >= 0; i = text.IndexOf(search, i + search.Length, StringComparison.Ordinal))
                {
                    occurrences++;
                }

                context.CheckLength(text.Length + (occurrences * (replacement.Length - search.Length)));
            }

            return DataValue.FromString(text.Replace(search, replacement, StringComparison.Ordinal));
        }

        private static DataValue Substring(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            string text = args[0].AsString();
            int start = ToIndex(args[1].AsNumber());
            if (start < 0)
            {
                start = Math.Max(0, text.Length + start);
            }

            if (start >= text.Length)
            {
                return DataValue.EmptyString;
            }

            int length = args.Length > 2 ? Math.Max(0, ToIndex(args[2].AsNumber())) : text.Length - start;
            length = Math.Min(length, text.Length - start);
            return DataValue.FromString(text.Substring(start, length));
        }

        private static int ToIndex(double value) => (int)Math.Clamp(Math.Truncate(value), int.MinValue / 2, int.MaxValue / 2);

        private static DataValue Join(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            IReadOnlyList<DataValue> list = args[0].AsList();
            string separator = args.Length > 1 ? args[1].AsString() : ", ";
            context.Consume(list.Count);
            return context.Text(DataValue.JoinList(list, separator));
        }

        /// <summary>
        /// Quote <paramref name="text"/> as one trigger-action argument: wrapped in double quotes, with inner double
        /// quotes escaped as <c>\"</c> (how the game's quote-aware argument splitter reads them).
        /// </summary>
        internal static string Quote(string text)
        {
            return "\"" + text.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }
    }
}
