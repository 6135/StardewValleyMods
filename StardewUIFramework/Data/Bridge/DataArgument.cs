using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Hosting;

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// A composite argument written in data (<c>Args</c>, or an extra field of a custom tag). It is stored in the
    /// <see cref="CompositeArgs"/> bag as is and converted to whatever the composite's builder asks for
    /// (<see cref="CompositeArgs"/> calls <see cref="TryConvert"/>):
    /// <list type="bullet">
    ///   <item>a reference (a plain state / model path such as <c>menu.volume</c>, <c>config[owner].x</c>, <c>model.settings.Day</c>, <c>@owner/name</c>): its value for text / number / bool / getters, a write to it for setters, the object for <c>GetObject</c>;</item>
    ///   <item>text with <c>${...}</c>: evaluated when read (getters are live);</item>
    ///   <item>other text, numbers and bools: literals (text a number is asked for is evaluated as an expression);</item>
    ///   <item>for <c>GetAction</c> (and setters of non-references): an action list (a string, an array or an action object), run in the element's scope with <c>event.value</c>.</item>
    /// </list>
    /// </summary>
    internal sealed class DataArgument
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();

        private readonly string key;
        private readonly JToken? token;
        private readonly DataScope scope;
        private readonly string? text;
        private readonly BindTarget? reference;
        private readonly Template? template;
        private List<ActionDefinition>? actions;
        private bool actionsRead;
        private bool reported;

        internal DataArgument(string key, JToken? token, DataScope scope)
        {
            this.key = key;
            this.token = token;
            this.scope = scope;
            if (token is { Type: JTokenType.String })
            {
                text = token.Value<string>() ?? string.Empty;
                if (ExpressionValueResolver.HasTemplate(text))
                {
                    template = Template.Parse(text);
                }
                else if (BindTarget.TryParse(text, scope, allowBare: false, out BindTarget? target, out _))
                {
                    reference = target;
                }
                else if (IsArgsPath(text))
                {
                    // a one-way argument forwarded from the enclosing template / composite (v1.7)
                    template = Template.Parse("${" + text.Trim() + "}");
                }
            }
        }

        private static bool IsArgsPath(string text)
        {
            string trimmed = text.Trim();
            return trimmed.StartsWith("args.", StringComparison.Ordinal) && trimmed.Length > 5 && trimmed.Skip(5).All(c => char.IsLetterOrDigit(c) || c is '_' or '.');
        }

        /// <summary>The state / model path the argument refers to (<c>menu.volume</c>), or null for literals and templates.</summary>
        internal BindTarget? Reference => reference;

        /// <summary>True when the value can change (a reference or a <c>${...}</c> template).</summary>
        internal bool IsDynamic => reference != null || template != null;

        /// <summary>The argument's current value.</summary>
        internal DataValue Current()
        {
            if (reference != null)
            {
                return reference.Read();
            }

            if (template != null)
            {
                ExpressionResult result = template.Evaluate(scope, ExpressionValueResolver.Functions);
                if (!result.Succeeded)
                {
                    Report(result.Error ?? "evaluation failed");
                }

                return result.Value;
            }

            if (text != null)
            {
                return DataValue.FromString(text.Replace("$${", "${", StringComparison.Ordinal));
            }

            return RowScope.FromJson(token);
        }

        private double CurrentNumber()
        {
            DataValue value = Current();
            if (value.Kind != DataKind.String || reference != null || template != null || DataValue.TryParseNumber(value.AsString().Trim(), out _))
            {
                return value.AsNumber();
            }

            // literal text asked for as a number: an expression ("menu.count * 2")
            DataValue evaluated = ExpressionValueResolver.Instance.Evaluate(text!, scope, out string? error);
            if (error != null)
            {
                Report(error);
            }

            return evaluated.AsNumber();
        }

        /// <summary>Convert to <paramref name="type"/> (the types <c>IUICompositeArgs</c> getters ask for); false when it has no such form.</summary>
        internal bool TryConvert(Type type, out object? result)
        {
            result = null;
            if (type == typeof(string))
            {
                result = Current().AsString();
            }
            else if (type == typeof(double) || type == typeof(double?))
            {
                result = CurrentNumber();
            }
            else if (type == typeof(bool) || type == typeof(bool?))
            {
                result = Current().AsBool();
            }
            else if (type == typeof(Func<string>))
            {
                result = (Func<string>)(() => Current().AsString());
            }
            else if (type == typeof(Func<double>))
            {
                result = (Func<double>)CurrentNumber;
            }
            else if (type == typeof(Action<string>))
            {
                List<ActionDefinition>? list = reference == null ? ActionsOrNull() : null;
                if (reference != null)
                {
                    result = (Action<string>)(v => Write(DataValue.FromString(v ?? string.Empty)));
                }
                else if (list != null)
                {
                    result = (Action<string>)(v => Run(list, v, DataValue.FromString(v)));
                }
            }
            else if (type == typeof(Action<double>))
            {
                List<ActionDefinition>? list = reference == null ? ActionsOrNull() : null;
                if (reference != null)
                {
                    result = (Action<double>)(v => Write(DataValue.FromNumber(v)));
                }
                else if (list != null)
                {
                    result = (Action<double>)(v => Run(list, null, DataValue.FromNumber(v)));
                }
            }
            else if (type == typeof(Action))
            {
                List<ActionDefinition>? list = ActionsOrNull();
                if (list != null)
                {
                    result = (Action)(() => Run(list, null, DataValue.Null));
                }
            }
            else if (type == typeof(object))
            {
                result = ModelAccessor.ToClr(Current()) ?? (object?)token?.ToString();
            }
            else
            {
                return false;
            }

            return result != null;
        }

        private void Write(DataValue value)
        {
            if (!reference!.Write(value, out string error))
            {
                Report($"could not write {reference}: {error}");
            }
        }

        private List<ActionDefinition>? ActionsOrNull()
        {
            if (!actionsRead)
            {
                actionsRead = true;
                try
                {
                    actions = ActionListConverter.Read(token, Serializer);
                }
                catch (Exception ex)
                {
                    Report($"is not an action list: {ex.Message}");
                }
            }

            return actions is { Count: > 0 } ? actions : null;
        }

        private void Run(List<ActionDefinition> list, object? eventArgs, DataValue value)
        {
            var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase) { ["value"] = value };
            DataActionRunner.RunGuarded(list, scope.WithEvent("Arg:" + key, eventArgs, fields), "Arg:" + key);
        }

        /// <summary>Log a problem of the argument, once (getters run every frame).</summary>
        private void Report(string message)
        {
            if (reported)
            {
                return;
            }

            reported = true;
            UIServices.Log($"[{scope.Owner}] {scope}: composite argument '{key}' {message} (logged once).", LogLevel.Warn);
        }

        public override string ToString() => token?.ToString(Formatting.None) ?? "null";
    }
}
