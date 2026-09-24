using System;
using System.Collections.Generic;
using System.Text;

namespace UIFramework.Data.Expressions
{
    /// <summary>One step of a scope path: a member name (<c>.x</c>, and the root) or an index key (<c>[x]</c>).</summary>
    internal readonly struct PathSegment
    {
        internal PathSegment(string key, bool isIndex)
        {
            Key = key;
            IsIndex = isIndex;
        }

        /// <summary>The member name or index key as text (numeric indexes use invariant round-trip text, e.g. <c>"0"</c>).</summary>
        internal string Key { get; }

        /// <summary>True when written as <c>[key]</c> (e.g. the qualifier in <c>menu[owner/menu].x</c>).</summary>
        internal bool IsIndex { get; }

        public override string ToString() => IsIndex ? "[" + Key + "]" : Key;

        /// <summary>Render a path back to source-like text (<c>menu[owner/menu].x</c>) for log messages.</summary>
        internal static string Format(IReadOnlyList<PathSegment> path)
        {
            StringBuilder builder = new();
            for (int i = 0; i < path.Count; i++)
            {
                if (path[i].IsIndex)
                {
                    builder.Append('[').Append(path[i].Key).Append(']');
                }
                else
                {
                    if (i > 0)
                    {
                        builder.Append('.');
                    }

                    builder.Append(path[i].Key);
                }
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Where an expression reads its data. The evaluator never touches state or game objects directly: every
    /// identifier path, member of an opaque object and <c>@owner/name(...)</c> call goes through a scope, which is how
    /// the data layer tracks dependencies and supplies <c>menu / session / player / stat / config / ctx / args / row /
    /// event / self / el / game / ui / model / @owner</c>.
    /// </summary>
    /// <remarks>
    /// Implementations must not throw (a throw is caught and reported as an evaluation error). Set
    /// <c>isVolatile</c> when the value can change without a state epoch bump (game time, hover, GSQ results): the
    /// cached result is then only reused within the same tick.
    /// </remarks>
    internal interface IExpressionScope
    {
        /// <summary>
        /// Resolve a whole identifier path, e.g. <c>menu.x</c> → <c>[menu, x]</c>, <c>menu[owner/menu].x</c> →
        /// <c>[menu, [owner/menu], x]</c>, <c>@owner/name.y</c> → <c>[@owner/name, y]</c>. Return false for unknown
        /// paths (the expression sees null). The list is only valid during the call.
        /// </summary>
        bool TryResolve(IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile);

        /// <summary>Read <paramref name="member"/> of an opaque object value (items, models). Lists, strings and string-keyed dictionaries are handled before this is asked.</summary>
        bool TryGetMember(DataValue target, PathSegment member, out DataValue value, out bool isVolatile);

        /// <summary>Call a C#-exposed function, <c>@owner/name(args)</c> (<paramref name="name"/> excludes the <c>@</c>). Return false when it doesn't exist.</summary>
        bool TryCallExternal(string name, ReadOnlySpan<DataValue> args, out DataValue result, out bool isVolatile);
    }

    /// <summary>Factory for the shared empty scope.</summary>
    internal static class ExpressionScope
    {
        /// <summary>A scope that resolves nothing (every path is null, every external call is unknown).</summary>
        internal static IExpressionScope Empty { get; } = new DictionaryScope();
    }

    /// <summary>
    /// A simple dictionary-backed scope for internal use (templates, one-off evaluations, local variables
    /// layered over another scope such as a Repeat's <c>As</c> name). Roots are matched ordinally; the rest of the
    /// path walks lists, strings and string-keyed dictionaries structurally. Unknown roots, members and external
    /// calls fall through to the <see cref="Parent"/> scope.
    /// </summary>
    internal sealed class DictionaryScope : IExpressionScope
    {
        private readonly Dictionary<string, Entry> roots = new(StringComparer.Ordinal);
        private Dictionary<string, Func<DataValue[], DataValue>>? externals;

        internal DictionaryScope(IExpressionScope? parent = null)
        {
            Parent = parent;
        }

        /// <summary>The scope consulted for anything this one doesn't define.</summary>
        internal IExpressionScope? Parent { get; }

        /// <summary>Define or replace root <paramref name="name"/>.</summary>
        internal DictionaryScope Set(string name, DataValue value, bool isVolatile = false)
        {
            roots[name] = new Entry(value, isVolatile);
            return this;
        }

        /// <summary>Define or replace root <paramref name="name"/> from a CLR value (see <see cref="DataValue.FromObject"/>).</summary>
        internal DictionaryScope Set(string name, object? value) => Set(name, DataValue.FromObject(value));

        /// <summary>Remove root <paramref name="name"/>.</summary>
        internal bool Remove(string name) => roots.Remove(name);

        /// <summary>Define an <c>@name(args)</c> function (the name excludes the <c>@</c>).</summary>
        internal DictionaryScope SetExternal(string name, Func<DataValue[], DataValue> function)
        {
            (externals ??= new Dictionary<string, Func<DataValue[], DataValue>>(StringComparer.OrdinalIgnoreCase))[name] = function;
            return this;
        }

        public bool TryResolve(IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile)
        {
            if (path.Count > 0 && roots.TryGetValue(path[0].Key, out Entry entry))
            {
                value = entry.Value;
                isVolatile = entry.IsVolatile;
                for (int i = 1; i < path.Count; i++)
                {
                    if (value.TryGetMember(path[i].Key, out DataValue next))
                    {
                        value = next;
                        continue;
                    }

                    if (value.Kind == DataKind.Object && TryGetMember(value, path[i], out next, out bool memberVolatile))
                    {
                        value = next;
                        isVolatile |= memberVolatile;
                        continue;
                    }

                    value = DataValue.Null;
                    return false;
                }

                return true;
            }

            if (Parent != null)
            {
                return Parent.TryResolve(path, out value, out isVolatile);
            }

            value = DataValue.Null;
            isVolatile = false;
            return false;
        }

        public bool TryGetMember(DataValue target, PathSegment member, out DataValue value, out bool isVolatile)
        {
            if (Parent != null)
            {
                return Parent.TryGetMember(target, member, out value, out isVolatile);
            }

            value = DataValue.Null;
            isVolatile = false;
            return false;
        }

        public bool TryCallExternal(string name, ReadOnlySpan<DataValue> args, out DataValue result, out bool isVolatile)
        {
            if (externals != null && externals.TryGetValue(name, out Func<DataValue[], DataValue>? function))
            {
                result = function(args.ToArray());
                isVolatile = false;
                return true;
            }

            if (Parent != null)
            {
                return Parent.TryCallExternal(name, args, out result, out isVolatile);
            }

            result = DataValue.Null;
            isVolatile = false;
            return false;
        }

        private readonly struct Entry
        {
            internal Entry(DataValue value, bool isVolatile)
            {
                Value = value;
                IsVolatile = isVolatile;
            }

            internal DataValue Value { get; }

            internal bool IsVolatile { get; }
        }
    }
}
