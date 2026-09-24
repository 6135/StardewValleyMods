using System;
using StardewModdingAPI;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The v1.4 value resolver: <c>${expr}</c> is live, <c>$:{expr}</c> is evaluated once per open, <c>$${</c> is a
    /// literal <c>${</c>. A field whose whole value is one <c>${...}</c> keeps the expression's type; bool and number
    /// fields also accept a bare expression (<c>"menu.count &gt; 3"</c>). Values that parse as a literal of the field's
    /// kind stay literals (no evaluation cost), constant expressions are folded at build time, and parse errors are
    /// reported at load with the value's path. Action strings are interpolated right before each runs.
    /// </summary>
    internal sealed class ExpressionValueResolver : IValueResolver
    {
        internal static readonly ExpressionValueResolver Instance = new();

        /// <summary>The functions data expressions can call (built-ins plus the game and UI functions).</summary>
        internal static FunctionRegistry Functions => FunctionRegistry.Default;

        public ValueSource<T>? Resolve<T>(string raw, ValueKind<T> kind, DataPath path, DataMessageLog log)
        {
            Template template;
            if (HasTemplate(raw) || raw.Contains("$${", StringComparison.Ordinal))
            {
                template = Template.Parse(raw); // an escaped "$${" alone gives a static template (unescaped below)
            }
            else if (kind.Parse(raw, out T literal))
            {
                return new LiteralSource<T>(literal);
            }
            else if (kind.AllowsBareExpression)
            {
                template = Template.ParseField(raw);
            }
            else
            {
                log.Error(path, $"'{raw}' is not {kind.Description}; the value is ignored.");
                return null;
            }

            if (template.Errors.Count > 0)
            {
                foreach (ParseError error in template.Errors)
                {
                    log.Error(path, $"expression error in '{raw}': {error}");
                }

                if (typeof(T) != typeof(string))
                {
                    return null; // text still renders (the broken part as written); typed values are skipped
                }
            }

            if (template.IsStatic)
            {
                string text = template.StaticText ?? string.Empty;
                if (kind.Parse(text, out T value))
                {
                    return new LiteralSource<T>(value);
                }

                log.Error(path, $"'{text}' is not {kind.Description}; the value is ignored.");
                return null;
            }

            if (template.IsConstant)
            {
                ExpressionResult result = template.Evaluate(ExpressionScope.Empty, Functions);
                if (result.Succeeded && TryConvert(result.Value, kind, out T folded))
                {
                    return new LiteralSource<T>(folded);
                }

                log.Error(path, result.Succeeded ? $"'{raw}' does not give {kind.Description}." : $"'{raw}': {result.Error}");
                return null;
            }

            return new ExpressionSource<T>(template, kind, path.ToString());
        }

        public string Interpolate(string raw, DataScope scope)
        {
            if (!HasTemplate(raw))
            {
                return raw.Contains("$${", StringComparison.Ordinal) ? raw.Replace("$${", "${", StringComparison.Ordinal) : raw;
            }

            ExpressionResult result = Template.Parse(raw).Evaluate(scope, Functions);
            if (!result.Succeeded)
            {
                UIServices.Log($"[{scope.Owner}] {scope}: '{raw}': {result.Error}", LogLevel.Warn);
            }

            return result.Value.AsString();
        }

        public DataValue Evaluate(string raw, DataScope scope, out string? error)
        {
            if (!HasTemplate(raw) && raw.Trim().ToLowerInvariant() is "yes" or "no")
            {
                error = null;
                return DataValue.FromBool(raw.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)); // the literal bool forms fields accept
            }

            Template template = Template.ParseField(raw);
            ExpressionResult result = template.Evaluate(scope, Functions);
            error = result.Error;
            return result.Value;
        }

        /// <summary>True when the text contains a <c>${</c> or <c>$:{</c> segment (an escaped <c>$${</c> alone is not one).</summary>
        internal static bool HasTemplate(string raw)
        {
            if (!raw.Contains('$', StringComparison.Ordinal))
            {
                return false;
            }

            string unescaped = raw.Replace("$${", string.Empty, StringComparison.Ordinal);
            return unescaped.Contains("${", StringComparison.Ordinal) || unescaped.Contains("$:{", StringComparison.Ordinal);
        }

        /// <summary>Convert an expression result to a field's type (bools by truthiness, numbers numerically, anything else through the field's parser).</summary>
        internal static bool TryConvert<T>(DataValue value, ValueKind<T> kind, out T result)
        {
            object? boxed = null;
            bool ok = true;
            if (typeof(T) == typeof(DataValue))
            {
                result = (T)(object)value;
                return true;
            }

            if (typeof(T) == typeof(bool))
            {
                boxed = value.AsBool();
            }
            else if (typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                double number;
                if (value.Kind is DataKind.Number or DataKind.Bool)
                {
                    number = value.AsNumber();
                }
                else if (value.IsNull || (value.Kind == DataKind.String && (value.AsString().Trim().Length == 0 || value.AsString().Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))))
                {
                    if (typeof(T) == typeof(int?))
                    {
                        result = default!;
                        return true;
                    }

                    ok = false;
                    number = 0;
                }
                else
                {
                    ok = DataValue.TryParseNumber(value.AsString().Trim(), out number);
                }

                if (typeof(T) == typeof(double))
                {
                    boxed = number;
                }
                else if (typeof(T) == typeof(float))
                {
                    boxed = (float)number;
                }
                else
                {
                    int rounded = (int)Math.Clamp(Math.Round(number, MidpointRounding.AwayFromZero), int.MinValue, int.MaxValue);
                    boxed = typeof(T) == typeof(int?) ? (int?)rounded : rounded;
                }
            }
            else if (typeof(T) == typeof(string))
            {
                boxed = value.AsString();
            }
            else if (value.AsObject() is T direct)
            {
                // an object of the field's own type (a C# row's Rectangle, Color, Texture2D...) is used as is
                result = direct;
                return true;
            }
            else
            {
                return kind.Parse(value.AsString(), out result);
            }

            result = ok ? (T)boxed! : default!;
            return ok;
        }
    }

    /// <summary>
    /// A live value: a template (or bare expression) evaluated in the scope it is read in, cached per screen and state
    /// epoch (and per tick when it read something volatile). One-time <c>$:{...}</c> segments are frozen per open of
    /// the scope's UI. A failing value keeps its last good value and is logged once.
    /// </summary>
    internal sealed class ExpressionSource<T> : ValueSource<T>
    {
        private readonly Template template;
        private readonly ValueKind<T> kind;
        private readonly string path;
        private readonly EvaluationCache cache = new();
        private Template? resolved;
        private int resolvedGeneration = -1;
        private T last = default!;
        private bool reported;

        internal ExpressionSource(Template template, ValueKind<T> kind, string path)
        {
            this.template = template;
            this.kind = kind;
            this.path = path;
        }

        internal override bool IsDynamic => true;

        internal override T Get(DataScope scope)
        {
            Template current = template;
            if (template.HasOneTime)
            {
                int generation = scope.Runtime?.OpenGeneration ?? 0;
                if (resolved == null || resolvedGeneration != generation)
                {
                    resolved = template.ResolveOneTime(scope, out string? oneTimeError, ExpressionValueResolver.Functions);
                    resolvedGeneration = generation;
                    cache.Clear();
                    if (oneTimeError != null)
                    {
                        Report(scope, oneTimeError);
                    }
                }

                current = resolved;
            }

            int screen = DataStateStore.Screen;
            long epoch = DataStateStore.Active?.Epoch(screen) ?? 0;
            ExpressionResult result = current.Evaluate(scope, cache, screen, epoch, DataEnvironment.Tick, ExpressionValueResolver.Functions);
            if (!result.Succeeded)
            {
                Report(scope, result.Error!);
                if (result.Value.IsNull)
                {
                    return last;
                }
            }

            if (ExpressionValueResolver.TryConvert(result.Value, kind, out T value))
            {
                last = value;
                return value;
            }

            Report(scope, $"'{result.Value.AsString()}' is not {kind.Description}");
            return last;
        }

        private void Report(DataScope scope, string error)
        {
            if (reported)
            {
                return;
            }

            reported = true;
            UIServices.Log($"[{scope.Owner}] {path}: {error} (logged once).", LogLevel.Warn);
        }
    }
}
