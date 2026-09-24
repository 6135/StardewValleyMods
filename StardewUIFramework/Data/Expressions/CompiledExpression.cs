using System;
using System.Collections.Generic;

namespace UIFramework.Data.Expressions
{
    /// <summary>Hard limits that keep content-pack expressions cheap and safe.</summary>
    internal static class ExpressionLimits
    {
        /// <summary>Maximum syntax-tree depth (and parser nesting).</summary>
        internal const int MaxDepth = 64;

        /// <summary>Maximum length of any string produced or parsed by an expression.</summary>
        internal const int MaxStringLength = 10000;

        /// <summary>Maximum number of elements when converting a CLR sequence into a list value.</summary>
        internal const int MaxListLength = 1000;

        /// <summary>Evaluation step budget per evaluation (one per node, plus per element for list built-ins).</summary>
        internal const int MaxSteps = 10000;

        /// <summary>Maximum length of one expression's source text.</summary>
        internal const int MaxSourceLength = 4096;

        /// <summary>Maximum length of a rendered template.</summary>
        internal const int MaxTemplateLength = 65536;

        /// <summary>Compiled expressions / templates kept in the parse caches before they are flushed.</summary>
        internal const int MaxCachedEntries = 4096;
    }

    /// <summary>A parse problem and where it happened (zero-based offset into the expression or template text).</summary>
    internal sealed class ParseError
    {
        internal ParseError(string message, int position)
        {
            Message = message;
            Position = position;
        }

        internal string Message { get; }

        internal int Position { get; }

        /// <summary>The same error shifted by <paramref name="offset"/> (used for expressions embedded in templates).</summary>
        internal ParseError WithOffset(int offset) => offset == 0 ? this : new ParseError(Message, Position + offset);

        public override string ToString() => $"{Message} (at position {Position})";
    }

    /// <summary>The outcome of an evaluation. Errors never throw; they come back here with a null <see cref="Value"/>.</summary>
    internal readonly struct ExpressionResult
    {
        internal ExpressionResult(DataValue value, bool isVolatile, string? error = null)
        {
            Value = value;
            IsVolatile = isVolatile;
            Error = error;
        }

        /// <summary>The result (null on error).</summary>
        internal DataValue Value { get; }

        /// <summary>True when a volatile source (per-tick value, volatile function) was read, so the result is only valid this tick.</summary>
        internal bool IsVolatile { get; }

        /// <summary>Parse or evaluation error message, or null on success.</summary>
        internal string? Error { get; }

        internal bool Succeeded => Error == null;

        internal static ExpressionResult Failure(string error, bool isVolatile = false) => new(DataValue.Null, isVolatile, error);
    }

    /// <summary>
    /// A parsed expression, ready to evaluate any number of times against different scopes. Obtain one with
    /// <see cref="Compile"/> (cached by source text); it never throws, check <see cref="Error"/>.
    /// </summary>
    internal sealed class CompiledExpression
    {
        private static readonly Dictionary<string, CompiledExpression> Cache = new(StringComparer.Ordinal);
        private static readonly object CacheLock = new();

        private bool constantEvaluated;
        private ExpressionResult constantResult;

        private CompiledExpression(string source, Node? root, ParseError? error)
        {
            Source = source;
            Root = root;
            Error = error;
            IsConstant = root != null && root.IsConstant;
        }

        /// <summary>The expression text as written.</summary>
        internal string Source { get; }

        /// <summary>The parse error, or null when the expression is valid.</summary>
        internal ParseError? Error { get; }

        internal bool IsValid => Error == null;

        /// <summary>True when the expression reads no scope and calls no function, so its value never changes (e.g. <c>2 * 8</c>, <c>'a' + 'b'</c>).</summary>
        internal bool IsConstant { get; }

        /// <summary>The syntax tree (null when <see cref="Error"/> is set).</summary>
        internal Node? Root { get; }

        // -------------------------------------------------------------------------------------------------------------
        //  Construction
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Parse <paramref name="source"/>, reusing an earlier parse of the same text.</summary>
        internal static CompiledExpression Compile(string? source)
        {
            source ??= string.Empty;
            lock (CacheLock)
            {
                if (Cache.TryGetValue(source, out CompiledExpression? cached))
                {
                    return cached;
                }
            }

            CompiledExpression compiled = Parse(source);
            lock (CacheLock)
            {
                if (Cache.Count >= ExpressionLimits.MaxCachedEntries)
                {
                    Cache.Clear();
                }

                Cache[source] = compiled;
            }

            return compiled;
        }

        /// <summary>Parse <paramref name="source"/> without touching the cache.</summary>
        internal static CompiledExpression Parse(string? source)
        {
            source ??= string.Empty;
            Parser.TryParse(source, out Node? root, out ParseError? error);
            return new CompiledExpression(source, root, error);
        }

        /// <summary>An expression that always yields <paramref name="value"/> (used to bake one-time template segments).</summary>
        internal static CompiledExpression Constant(DataValue value, string source)
        {
            return new CompiledExpression(source, new LiteralNode(value, 0), null);
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Evaluation
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Evaluate against <paramref name="scope"/> using <paramref name="functions"/> (default: <see cref="FunctionRegistry.Default"/>).</summary>
        internal ExpressionResult Evaluate(IExpressionScope scope, FunctionRegistry? functions = null)
        {
            if (Root == null)
            {
                return ExpressionResult.Failure(Error?.ToString() ?? "Invalid expression.");
            }

            if (IsConstant)
            {
                if (!constantEvaluated)
                {
                    constantResult = Evaluator.Run(Root, scope, functions ?? FunctionRegistry.Default);
                    constantEvaluated = true;
                }

                return constantResult;
            }

            return Evaluator.Run(Root, scope, functions ?? FunctionRegistry.Default);
        }

        /// <summary>
        /// Evaluate through <paramref name="cache"/>: the previous result is reused while the scope, screen and
        /// <paramref name="epoch"/> are unchanged, and, when the previous evaluation read a volatile source, the
        /// <paramref name="tick"/> too. The caller picks the epoch (e.g. the state store's change counter).
        /// </summary>
        internal ExpressionResult Evaluate(IExpressionScope scope, EvaluationCache cache, int screen, long epoch, long tick, FunctionRegistry? functions = null)
        {
            if (cache.TryGet(scope, screen, epoch, tick, out ExpressionResult cached))
            {
                return cached;
            }

            ExpressionResult result = Evaluate(scope, functions);
            cache.Store(scope, screen, epoch, tick, result);
            return result;
        }

        public override string ToString() => Source;
    }

    /// <summary>
    /// A per-binding result cache keyed by <c>(scope, screen, epoch, tick-when-volatile)</c>. Compiled expressions and
    /// templates are shared between every element using the same text, so the cache lives with the binding instead.
    /// </summary>
    internal sealed class EvaluationCache
    {
        private Slot[] slots = new Slot[1];

        /// <summary>Return the stored result when it is still valid for these keys.</summary>
        internal bool TryGet(IExpressionScope scope, int screen, long epoch, long tick, out ExpressionResult result)
        {
            if (screen >= 0 && screen < slots.Length)
            {
                ref Slot slot = ref slots[screen];
                if (slot.HasValue && ReferenceEquals(slot.Scope, scope) && slot.Epoch == epoch && (!slot.Result.IsVolatile || slot.Tick == tick))
                {
                    result = slot.Result;
                    return true;
                }
            }

            result = default;
            return false;
        }

        /// <summary>Remember <paramref name="result"/> for these keys.</summary>
        internal void Store(IExpressionScope scope, int screen, long epoch, long tick, in ExpressionResult result)
        {
            if (screen < 0)
            {
                return;
            }

            if (screen >= slots.Length)
            {
                Array.Resize(ref slots, Math.Max(screen + 1, slots.Length * 2));
            }

            slots[screen] = new Slot
            {
                HasValue = true,
                Scope = scope,
                Epoch = epoch,
                Tick = tick,
                Result = result
            };
        }

        /// <summary>Forget every stored result.</summary>
        internal void Clear() => Array.Clear(slots, 0, slots.Length);

        private struct Slot
        {
            internal bool HasValue;
            internal IExpressionScope? Scope;
            internal long Epoch;
            internal long Tick;
            internal ExpressionResult Result;
        }
    }
}
