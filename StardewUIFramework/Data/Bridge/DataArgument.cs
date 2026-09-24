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

        /// <summary>The raw JSON of the argument.</summary>
        internal JToken? Token => token;

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

        private void Report(string message)
        {
            UIServices.Log($"[{scope.Owner}] {scope}: composite argument '{key}' {message}", LogLevel.Warn);
        }

        public override string ToString() => token?.ToString(Formatting.None) ?? "null";
    }

    /// <summary>
    /// A value an input (or composite setter, or <see cref="DataCall.SetState"/>) reads and writes: a state value
    /// (<see cref="StateAddress"/>), a member path of a model exposed from C# (<c>model.settings.Day</c>,
    /// <c>model[owner/name].Day</c>, <c>@owner/name.Day</c>) or an exposed signal (<c>@owner/name</c>).
    /// </summary>
    internal sealed class BindTarget
    {
        private readonly StateAddress address;
        private readonly string? hookKey;
        private readonly string[] members;
        private readonly string description;
        private readonly Func<DataValue>? reader;
        private readonly Func<DataValue, string?>? writer;

        private BindTarget(StateAddress address, string? hookKey, string[] members, string description, Func<DataValue>? reader = null, Func<DataValue, string?>? writer = null)
        {
            this.address = address;
            this.hookKey = hookKey;
            this.members = members;
            this.description = description;
            this.reader = reader;
            this.writer = writer;
        }

        /// <summary>True for a state value (<see cref="Address"/>); false for a model / exposed value or a delegate pair.</summary>
        internal bool IsState => hookKey == null && reader == null;

        /// <summary>A target read and written through delegates (v1.7: a two-way template / composite argument from C#); <paramref name="write"/> returns an error or null.</summary>
        internal static BindTarget ForDelegates(Func<DataValue> read, Func<DataValue, string?> write, string description) => new(default, null, Array.Empty<string>(), description, read, write);

        /// <summary>The state address (only meaningful when <see cref="IsState"/>).</summary>
        internal StateAddress Address => address;

        /// <summary>A state target for <paramref name="address"/>.</summary>
        internal static BindTarget ForState(StateAddress address) => new(address, null, Array.Empty<string>(), address.ToString());

        /// <summary>True when <paramref name="raw"/> starts like a model / exposed-value path (not a state key).</summary>
        internal static bool IsModelSyntax(string? raw)
        {
            string text = raw?.Trim() ?? string.Empty;
            return text.StartsWith("model.", StringComparison.Ordinal) || text.StartsWith("model[", StringComparison.Ordinal) || text.StartsWith('@');
        }

        /// <summary>Parse a bind key in <paramref name="scope"/> (relative <c>.x</c> keys expand through <c>With</c>).</summary>
        internal static bool TryParse(string? raw, DataScope scope, bool allowBare, [NotNullWhen(true)] out BindTarget? target, out string error)
        {
            target = null;
            string text = raw?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                error = "the key is empty.";
                return false;
            }

            CompiledExpression expression = CompiledExpression.Compile(text);
            if (expression.Root is not PathNode { StaticPath: { } relative })
            {
                error = expression.Error != null ? $"'{text}' is not a state key: {expression.Error}" : $"'{text}' is not a state key (expected e.g. menu.name, config.x or model.settings.Day).";
                return false;
            }

            PathSegment[] path = scope.ExpandRelative(relative);
            string root = path[0].Key;

            // args.<name> (v1.7): the argument of the enclosing template / data composite, when it is two-way
            if (root == "args" && !path[0].IsIndex && scope.Locals != null && scope.Locals.TryGetValue("args", out DataValue argsValue)
                && argsValue.AsObject() is Building.TemplateArgs args)
            {
                if (path.Length != 2)
                {
                    error = $"'{text}' must name one argument (args.<name>).";
                    return false;
                }

                return args.TryBind(path[1].Key, out target, out error);
            }

            if (root == "model" && !path[0].IsIndex)
            {
                if (path.Length < 2)
                {
                    error = $"'{text}' names no model (model.<name>.<member>).";
                    return false;
                }

                string key = HookRegistry.Qualify(path[1].Key, scope.Owner);
                target = new BindTarget(default, key, path.Skip(2).Select(p => p.Key).ToArray(), text);
                error = string.Empty;
                return true;
            }

            if (root.StartsWith('@'))
            {
                target = new BindTarget(default, HookRegistry.Qualify(root, scope.Owner), path.Skip(1).Select(p => p.Key).ToArray(), text);
                error = string.Empty;
                return true;
            }

            if (!StateAddress.TryFromPath(path, scope, allowBare, out StateAddress stateAddress, out error))
            {
                return false;
            }

            target = ForState(stateAddress);
            return true;
        }

        /// <summary>The current value (null when a model or member is missing).</summary>
        internal DataValue Read()
        {
            if (reader != null)
            {
                return reader();
            }

            if (hookKey == null)
            {
                return DataStateStore.Active?.Read(address) ?? DataValue.Null;
            }

            HookRegistry? hooks = UIServices.Hooks;
            if (hooks == null)
            {
                return DataValue.Null;
            }

            object? current = hooks.ModelOf(hookKey);
            if (current == null)
            {
                if (members.Length == 0 && hooks.TryReadValue(hookKey, out DataValue exposed, out _))
                {
                    return exposed;
                }

                return DataValue.Null;
            }

            foreach (string member in members)
            {
                if (current == null || !ModelAccessor.TryGetRaw(current, member, out current))
                {
                    return DataValue.Null;
                }
            }

            return ModelAccessor.ToValue(current);
        }

        /// <summary>Write a value; false (with an error) when the target cannot hold it.</summary>
        internal bool Write(DataValue value, out string error)
        {
            if (reader != null)
            {
                string? failure = writer?.Invoke(value) ?? (writer == null ? "the value is read-only." : null);
                error = failure ?? string.Empty;
                return failure == null;
            }

            if (hookKey == null)
            {
                if (DataStateStore.Active == null)
                {
                    error = "data state is not available.";
                    return false;
                }

                return DataStateStore.Active.Write(address, value, out error);
            }

            HookRegistry? hooks = UIServices.Hooks;
            object? model = hooks?.ModelOf(hookKey);
            if (model != null)
            {
                return ModelAccessor.TrySetPath(model, members, value, out error);
            }

            if (members.Length == 0 && hooks?.SignalOf(hookKey) is { } signal)
            {
                signal.Value = value.AsString();
                error = string.Empty;
                return true;
            }

            error = $"nothing is exposed as '{hookKey}' (ExposeModel / ExposeSignal).";
            return false;
        }

        public override string ToString() => description;
    }
}
