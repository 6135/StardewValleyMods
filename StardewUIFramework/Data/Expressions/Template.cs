using System;
using System.Collections.Generic;
using System.Text;

namespace UIFramework.Data.Expressions
{
    /// <summary>One piece of a <see cref="Template"/>: literal text or an embedded expression.</summary>
    internal readonly struct TemplateSegment
    {
        internal TemplateSegment(string literal)
        {
            Literal = literal;
            Expression = null;
            IsOneTime = false;
            RawText = literal;
        }

        internal TemplateSegment(CompiledExpression expression, bool isOneTime, string rawText)
        {
            Literal = null;
            Expression = expression;
            IsOneTime = isOneTime;
            RawText = rawText;
        }

        /// <summary>Literal text (null for expression segments).</summary>
        internal string? Literal { get; }

        /// <summary>The embedded expression (null for literal segments).</summary>
        internal CompiledExpression? Expression { get; }

        /// <summary>True for <c>$:{expr}</c>: evaluated once per open (see <see cref="Template.ResolveOneTime"/>).</summary>
        internal bool IsOneTime { get; }

        /// <summary>The segment exactly as written (<c>${...}</c> included); rendered in place of a segment that failed to parse.</summary>
        internal string RawText { get; }

        internal bool IsExpression => Expression != null;
    }

    /// <summary>
    /// Text with embedded expressions: <c>${expr}</c> is live, <c>$:{expr}</c> is evaluated once, <c>$${</c> is a
    /// literal <c>${</c>. A template whose whole value is one <c>${...}</c> keeps the expression's type (bool, number).
    /// Parsing never throws; problems are listed in <see cref="Errors"/>.
    /// </summary>
    internal sealed class Template
    {
        private static readonly Dictionary<string, Template> TextCache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Template> FieldCache = new(StringComparer.Ordinal);
        // see CompiledExpression.CacheLock: uncontended on the game's main thread, safe for any other thread
        private static readonly object CacheLock = new();

        [ThreadStatic] private static StringBuilder? sharedBuilder;

        private readonly TemplateSegment[] segments;

        private Template(string text, TemplateSegment[] segments, IReadOnlyList<ParseError> errors)
        {
            Text = text;
            this.segments = segments;
            Errors = errors;

            bool hasExpression = false;
            bool constant = true;
            foreach (TemplateSegment segment in segments)
            {
                if (segment.Expression == null)
                {
                    continue;
                }

                hasExpression = true;
                HasOneTime |= segment.IsOneTime;
                constant &= !segment.IsOneTime && segment.Expression.IsConstant;
            }

            IsStatic = !hasExpression;
            IsConstant = constant;
            IsSingleExpression = segments.Length == 1 && segments[0].IsExpression;
            StaticText = IsStatic ? ConcatLiterals(segments) : null;
        }

        /// <summary>The template text as written.</summary>
        internal string Text { get; }

        /// <summary>Literal and expression pieces, in order.</summary>
        internal IReadOnlyList<TemplateSegment> Segments => segments;

        /// <summary>Parse problems (positions are offsets into <see cref="Text"/>).</summary>
        internal IReadOnlyList<ParseError> Errors { get; }

        /// <summary>True when there is no expression at all (<see cref="StaticText"/> is the value; <c>$${</c> already unescaped).</summary>
        internal bool IsStatic { get; }

        /// <summary>The literal value when <see cref="IsStatic"/>, else null.</summary>
        internal string? StaticText { get; }

        /// <summary>True when every expression is constant and none is one-time (apply once, never refresh).</summary>
        internal bool IsConstant { get; }

        /// <summary>True when any <c>$:{...}</c> segment is present.</summary>
        internal bool HasOneTime { get; }

        /// <summary>True when the whole value is one expression, whose typed result <see cref="Evaluate(IExpressionScope, FunctionRegistry?)"/> keeps.</summary>
        internal bool IsSingleExpression { get; }

        // -------------------------------------------------------------------------------------------------------------
        //  Parsing
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>Parse a text field (<c>${}</c> / <c>$:{}</c> segments inside literal text), reusing earlier parses.</summary>
        internal static Template Parse(string? text)
        {
            text ??= string.Empty;
            return GetOrAdd(TextCache, text, ParseText);
        }

        /// <summary>
        /// Parse a bool / number (or other typed) field: text containing <c>${</c> or <c>$:{</c> is a template, anything
        /// else is one bare expression (<c>"menu.count &gt; 3"</c>, <c>"true"</c>, <c>"8"</c>).
        /// </summary>
        internal static Template ParseField(string? text)
        {
            text ??= string.Empty;
            if (text.Contains("${", StringComparison.Ordinal) || text.Contains("$:{", StringComparison.Ordinal))
            {
                return Parse(text);
            }

            return GetOrAdd(FieldCache, text, ParseBareExpression);
        }

        private static Template GetOrAdd(Dictionary<string, Template> cache, string text, Func<string, Template> parse)
        {
            lock (CacheLock)
            {
                if (cache.TryGetValue(text, out Template? cached))
                {
                    return cached;
                }
            }

            Template template = parse(text);
            lock (CacheLock)
            {
                if (cache.Count >= ExpressionLimits.MaxCachedEntries)
                {
                    cache.Clear();
                }

                cache[text] = template;
            }

            return template;
        }

        private static Template ParseBareExpression(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Template(text, new[] { new TemplateSegment(text) }, Array.Empty<ParseError>());
            }

            CompiledExpression expression = CompiledExpression.Compile(text);
            IReadOnlyList<ParseError> errors = expression.Error != null ? new[] { expression.Error } : Array.Empty<ParseError>();
            return new Template(text, new[] { new TemplateSegment(expression, false, text) }, errors);
        }

        private static Template ParseText(string text)
        {
            List<TemplateSegment> segments = new();
            List<ParseError>? errors = null;
            StringBuilder literal = new();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c != '$')
                {
                    literal.Append(c);
                    i++;
                    continue;
                }

                if (StartsWith(text, i, "$${"))
                {
                    literal.Append("${");
                    i += 3;
                    continue;
                }

                bool oneTime;
                int expressionStart;
                if (StartsWith(text, i, "${"))
                {
                    oneTime = false;
                    expressionStart = i + 2;
                }
                else if (StartsWith(text, i, "$:{"))
                {
                    oneTime = true;
                    expressionStart = i + 3;
                }
                else
                {
                    literal.Append(c);
                    i++;
                    continue;
                }

                int close = FindClosingBrace(text, expressionStart);
                if (close < 0)
                {
                    (errors ??= new List<ParseError>()).Add(new ParseError(oneTime ? "Unterminated '$:{'." : "Unterminated '${'.", i));
                    literal.Append(text, i, text.Length - i);
                    break;
                }

                if (literal.Length > 0)
                {
                    segments.Add(new TemplateSegment(literal.ToString()));
                    literal.Clear();
                }

                string source = text.Substring(expressionStart, close - expressionStart);
                CompiledExpression expression = CompiledExpression.Compile(source);
                if (expression.Error != null)
                {
                    (errors ??= new List<ParseError>()).Add(expression.Error.WithOffset(expressionStart));
                }

                segments.Add(new TemplateSegment(expression, oneTime, text.Substring(i, close + 1 - i)));
                i = close + 1;
            }

            if (literal.Length > 0 || segments.Count == 0)
            {
                segments.Add(new TemplateSegment(literal.ToString()));
            }

            return new Template(text, segments.ToArray(), (IReadOnlyList<ParseError>?)errors ?? Array.Empty<ParseError>());
        }

        private static bool StartsWith(string text, int index, string value) => string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

        /// <summary>Index of the <c>}</c> closing an expression that starts at <paramref name="start"/>, skipping nested braces and quoted strings; -1 if none.</summary>
        private static int FindClosingBrace(string text, int start)
        {
            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '"':
                    case '\'':
                        for (i++; i < text.Length && text[i] != c; i++)
                        {
                            if (text[i] == '\\')
                            {
                                i++;
                            }
                        }

                        if (i >= text.Length)
                        {
                            return -1;
                        }

                        break;
                    case '{':
                        depth++;
                        break;
                    case '}':
                        if (depth == 0)
                        {
                            return i;
                        }

                        depth--;
                        break;
                }
            }

            return -1;
        }

        private static string ConcatLiterals(TemplateSegment[] segments)
        {
            if (segments.Length == 1)
            {
                return segments[0].Literal ?? string.Empty;
            }

            StringBuilder builder = new();
            foreach (TemplateSegment segment in segments)
            {
                builder.Append(segment.Literal);
            }

            return builder.ToString();
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Evaluation
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A copy with every <c>$:{...}</c> segment evaluated now and frozen (call once per open, then evaluate the
        /// copy live). Returns this template when it has no one-time segments. The first evaluation error, if any, is
        /// reported through <paramref name="error"/> (the failed segment renders empty).
        /// </summary>
        internal Template ResolveOneTime(IExpressionScope scope, out string? error, FunctionRegistry? functions = null)
        {
            error = null;
            if (!HasOneTime)
            {
                return this;
            }

            TemplateSegment[] resolved = new TemplateSegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                TemplateSegment segment = segments[i];
                if (!segment.IsOneTime || segment.Expression == null)
                {
                    resolved[i] = segment;
                    continue;
                }

                if (!segment.Expression.IsValid)
                {
                    resolved[i] = new TemplateSegment(segment.RawText);
                    continue;
                }

                ExpressionResult result = segment.Expression.Evaluate(scope, functions);
                error ??= result.Error;
                resolved[i] = IsSingleExpression
                    ? new TemplateSegment(CompiledExpression.Constant(result.Value, segment.Expression.Source), false, segment.RawText)
                    : new TemplateSegment(result.Value.AsString());
            }

            return new Template(Text, resolved, Errors);
        }

        /// <summary>
        /// Evaluate: a single-expression template returns the expression's typed value; anything else renders to a
        /// string. One-time segments not yet resolved are evaluated like live ones. Segments that failed to parse
        /// render as written; evaluation errors render empty and the first one is reported in the result.
        /// </summary>
        internal ExpressionResult Evaluate(IExpressionScope scope, FunctionRegistry? functions = null)
        {
            if (IsStatic)
            {
                return new ExpressionResult(DataValue.FromString(StaticText), false, Errors.Count > 0 ? Errors[0].ToString() : null);
            }

            if (IsSingleExpression)
            {
                return segments[0].Expression!.Evaluate(scope, functions);
            }

            StringBuilder builder = sharedBuilder ?? new StringBuilder();
            sharedBuilder = null; // nested evaluations (a scope rendering another template) get their own builder
            try
            {
                bool isVolatile = false;
                string? error = Errors.Count > 0 ? Errors[0].ToString() : null;
                foreach (TemplateSegment segment in segments)
                {
                    if (segment.Expression == null)
                    {
                        builder.Append(segment.Literal);
                    }
                    else if (!segment.Expression.IsValid)
                    {
                        builder.Append(segment.RawText);
                    }
                    else
                    {
                        ExpressionResult result = segment.Expression.Evaluate(scope, functions);
                        isVolatile |= result.IsVolatile;
                        error ??= result.Error;
                        builder.Append(result.Value.AsString());
                    }

                    if (builder.Length > ExpressionLimits.MaxTemplateLength)
                    {
                        builder.Length = ExpressionLimits.MaxTemplateLength;
                        error ??= $"Rendered text is longer than {ExpressionLimits.MaxTemplateLength} characters and was cut.";
                        break;
                    }
                }

                return new ExpressionResult(DataValue.FromString(builder.ToString()), isVolatile, error);
            }
            finally
            {
                builder.Clear();
                if (builder.Capacity <= 4096)
                {
                    sharedBuilder = builder;
                }
            }
        }

        /// <summary>Evaluate through <paramref name="cache"/> (see <see cref="CompiledExpression.Evaluate(IExpressionScope, EvaluationCache, int, long, long, FunctionRegistry?)"/>).</summary>
        internal ExpressionResult Evaluate(IExpressionScope scope, EvaluationCache cache, int screen, long epoch, long tick, FunctionRegistry? functions = null)
        {
            if (IsStatic)
            {
                return Evaluate(scope, functions);
            }

            if (cache.TryGet(scope, screen, epoch, tick, out ExpressionResult cached))
            {
                return cached;
            }

            ExpressionResult result = Evaluate(scope, functions);
            cache.Store(scope, screen, epoch, tick, result);
            return result;
        }

        public override string ToString() => Text;
    }
}
