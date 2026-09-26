using System;
using System.Collections.Generic;

namespace UIFramework.Data.Expressions
{
    /// <summary>
    /// Implementation of an expression function. <paramref name="args"/> is only valid during the call (it points
    /// into the evaluator's reused argument stack); copy values out if you keep them. Throwing is allowed and becomes
    /// an evaluation error; prefer <see cref="ExpressionContext.Fail"/> for a clean message.
    /// </summary>
    internal delegate DataValue ExpressionFunction(ExpressionContext context, ReadOnlySpan<DataValue> args);

    /// <summary>A named function with its arity.</summary>
    internal sealed class FunctionDefinition
    {
        internal FunctionDefinition(string name, int minArguments, int maxArguments, ExpressionFunction invoke, bool isVolatile)
        {
            Name = name;
            MinArguments = minArguments;
            MaxArguments = maxArguments;
            Invoke = invoke;
            IsVolatile = isVolatile;
        }

        internal string Name { get; }

        internal int MinArguments { get; }

        /// <summary>Maximum argument count, or -1 for unlimited.</summary>
        internal int MaxArguments { get; }

        internal ExpressionFunction Invoke { get; }

        /// <summary>When true every call marks the evaluation volatile (e.g. <c>gsq</c>, <c>hovered</c>), so results are reused within one tick only.</summary>
        internal bool IsVolatile { get; }
    }

    /// <summary>
    /// A case-insensitive table of expression functions. <see cref="Default"/> holds the pure built-ins (see
    /// <see cref="BuiltinFunctions"/>); the data layer registers the game-dependent ones (<c>gsq token loc isOpen
    /// focused hovered bounds itemName</c>) and C#-registered consumer functions into it.
    /// </summary>
    internal sealed class FunctionRegistry
    {
        private static FunctionRegistry? defaultRegistry;

        private readonly Dictionary<string, FunctionDefinition> functions = new(StringComparer.OrdinalIgnoreCase);
        private int version;

        /// <summary>The shared registry, pre-filled with the built-ins.</summary>
        internal static FunctionRegistry Default
        {
            get
            {
                if (defaultRegistry == null)
                {
                    FunctionRegistry registry = new();
                    BuiltinFunctions.RegisterAll(registry);
                    defaultRegistry = registry;
                }

                return defaultRegistry;
            }
        }

        /// <summary>Changes whenever a function is registered (invalidates call-site lookup caches).</summary>
        internal int Version => version;

        /// <summary>
        /// Register (or replace) <paramref name="name"/>. <paramref name="maxArguments"/> of -1 means unlimited.
        /// Returns false when the name is not a valid identifier or the arity is inconsistent.
        /// </summary>
        internal bool Register(string name, int minArguments, int maxArguments, ExpressionFunction function, bool isVolatile = false)
        {
            if (!IsValidName(name) || function == null || minArguments < 0 || (maxArguments >= 0 && maxArguments < minArguments))
            {
                return false;
            }

            functions[name] = new FunctionDefinition(name, minArguments, maxArguments, function, isVolatile);
            version++;
            return true;
        }

        /// <summary>Find <paramref name="name"/>.</summary>
        internal bool TryGet(string name, out FunctionDefinition? function) => functions.TryGetValue(name, out function);

        private static bool IsValidName(string? name)
        {
            if (string.IsNullOrEmpty(name) || !(char.IsLetter(name[0]) || name[0] == '_'))
            {
                return false;
            }

            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
