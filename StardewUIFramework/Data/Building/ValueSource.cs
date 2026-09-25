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
}
