using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;

namespace UIFramework.Data.Building
{
    /// <summary>Parses the text of a data field into a typed value.</summary>
    internal delegate bool ValueParser<T>(string text, out T value);

    /// <summary>A value type a data field can hold: its parser plus a description for messages ("a whole number").</summary>
    internal sealed class ValueKind<T>
    {
        internal ValueKind(string description, ValueParser<T> parse, bool allowsBareExpression = false)
        {
            Description = description;
            Parse = parse;
            AllowsBareExpression = allowsBareExpression;
        }

        /// <summary>True for bool / number fields, which also accept a bare expression (<c>"menu.count &gt; 3"</c>) besides <c>${...}</c>.</summary>
        internal bool AllowsBareExpression { get; }

        /// <summary>What the field expects, for messages.</summary>
        internal string Description { get; }

        /// <summary>The literal parser.</summary>
        internal ValueParser<T> Parse { get; }
    }

    /// <summary>
    /// Where a data field's value comes from: a constant (<see cref="LiteralSource{T}"/>) or, from v1.4, a live
    /// <c>${expression}</c> (<see cref="ExpressionSource{T}"/>) whose <see cref="IsDynamic"/> is true. The builder applies static values once and
    /// registers dynamic ones with the menu's refresh list (<see cref="DataRuntime.Refreshers"/>), so any property
    /// can become dynamic without changing the builder.
    /// </summary>
    internal abstract class ValueSource<T>
    {
        /// <summary>True when the value can change after the build (it is re-applied by the menu's data refresh).</summary>
        internal abstract bool IsDynamic { get; }

        /// <summary>The current value in <paramref name="scope"/>.</summary>
        internal abstract T Get(DataScope scope);
    }

    /// <summary>A constant.</summary>
    internal sealed class LiteralSource<T> : ValueSource<T>
    {
        private readonly T value;

        internal LiteralSource(T value)
        {
            this.value = value;
        }

        internal override bool IsDynamic => false;

        internal override T Get(DataScope scope) => value;
    }

    /// <summary>
    /// Turns the raw text of a data field into a <see cref="ValueSource{T}"/>, and interpolates action strings right
    /// before they run. This is the one place where values are interpreted: v1.4 swaps in an expression-aware
    /// resolver (<c>${...}</c>, <c>$:{...}</c>) through <see cref="DataService.Resolver"/>.
    /// </summary>
    internal interface IValueResolver
    {
        /// <summary>The source for <paramref name="raw"/>, or null (with a message in <paramref name="log"/>) when it is not a valid <paramref name="kind"/>.</summary>
        ValueSource<T>? Resolve<T>(string raw, ValueKind<T> kind, DataPath path, DataMessageLog log);

        /// <summary>The text of an action entry as it should run now.</summary>
        string Interpolate(string raw, DataScope scope);

        /// <summary>Evaluate a bare expression or template now (<c>When</c>, <c>If</c>, <c>Switch</c>, <c>Validate</c>...); errors come back in <paramref name="error"/>.</summary>
        DataValue Evaluate(string raw, DataScope scope, out string? error);
    }

    /// <summary>The v1.3 resolver (kept for tests and as the fallback): every value is a literal. <c>$${</c> is unescaped to <c>${</c>; an unescaped <c>${</c> is used as is, with a hint.</summary>
    internal sealed class LiteralValueResolver : IValueResolver
    {
        internal static readonly LiteralValueResolver Instance = new();

        public ValueSource<T>? Resolve<T>(string raw, ValueKind<T> kind, DataPath path, DataMessageLog log)
        {
            if (HasExpression(raw))
            {
                log.Info(path, "expressions (${...}) are supported from UI Framework 1.4; the text is used literally.");
            }

            string text = Unescape(raw);
            if (kind.Parse(text, out T value))
            {
                return new LiteralSource<T>(value);
            }

            log.Error(path, $"'{raw}' is not {kind.Description}; the value is ignored.");
            return null;
        }

        public string Interpolate(string raw, DataScope scope) => Unescape(raw);

        public DataValue Evaluate(string raw, DataScope scope, out string? error)
        {
            error = null;
            return State.StateAddress.Infer(Unescape(raw));
        }

        /// <summary>True when the text contains an unescaped <c>${</c>.</summary>
        internal static bool HasExpression(string raw) => raw.Replace("$${", string.Empty).Contains("${");

        private static string Unescape(string raw) => raw.Contains("$${") ? raw.Replace("$${", "${") : raw;
    }
}
