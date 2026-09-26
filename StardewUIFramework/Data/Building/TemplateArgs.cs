using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Bridge;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// <c>args.*</c> of a template or data composite body (v1.7): the instance's arguments (data values converted on
    /// each read, or what C# stored with <c>IUICompositeArgs</c>), else the parameter's <c>Default</c>, converted to the
    /// parameter's <c>Type</c>. <c>Bind: "args.x"</c> writes through when the argument is two-way: a data reference
    /// (<c>"menu.amount"</c>), or from C# an <see cref="IUISignal"/> or a getter with a setter stored at <c>x.set</c>.
    /// </summary>
    internal sealed class TemplateArgs
    {
        private readonly Dictionary<string, ParamDefinition>? parameters;
        private readonly CompositeArgs values;

        internal TemplateArgs(string name, Dictionary<string, ParamDefinition>? parameters, CompositeArgs values, Composite? host)
        {
            Name = name;
            this.parameters = parameters;
            this.values = values;
            Host = host;
        }

        /// <summary>The template or composite name.</summary>
        internal string Name { get; }

        /// <summary>The composite whose body this is (also for templates expanded inside it), or null; <c>_Publish</c> raises its events.</summary>
        internal Composite? Host { get; }

        /// <summary>The <c>args</c> of <paramref name="scope"/>, or null outside a body.</summary>
        internal static TemplateArgs? Of(DataScope? scope)
        {
            return scope?.Locals != null && scope.Locals.TryGetValue("args", out DataValue value) ? value.AsObject() as TemplateArgs : null;
        }

        /// <summary>The required parameters no argument sets.</summary>
        internal IEnumerable<string> MissingRequired()
        {
            if (parameters == null)
            {
                yield break;
            }

            foreach ((string name, ParamDefinition? param) in parameters)
            {
                if (param != null && IsRequired(param) && !values.TryGetRaw(name, out _))
                {
                    yield return name;
                }
            }
        }

        /// <summary>True when <paramref name="param"/> is Required.</summary>
        internal static bool IsRequired(ParamDefinition param) => param.Required != null && ValueParsers.Bool.Parse(param.Required, out bool required) && required;

        /// <summary>The value of argument <paramref name="key"/> (false when it is neither set nor a parameter).</summary>
        internal bool TryGet(string key, out DataValue value, out bool isVolatile)
        {
            isVolatile = false;
            ParamDefinition? param = Param(key);
            if (values.TryGetRaw(key, out object? raw))
            {
                value = Coerce(ToValue(raw, out isVolatile), param?.Type);
                return true;
            }

            if (param != null)
            {
                value = Coerce(param.Default == null ? DataValue.Null : RowScope.FromJson(param.Default), param.Type);
                return true;
            }

            value = DataValue.Null;
            return false;
        }

        /// <summary>A two-way target for <c>Bind: "args.&lt;key&gt;"</c>; false (with an error) when the argument is not two-way.</summary>
        internal bool TryBind(string key, out BindTarget? target, out string error)
        {
            target = null;
            error = string.Empty;
            values.TryGetRaw(key, out object? raw);
            switch (raw)
            {
                case DataArgument { Reference: { } reference }:
                    target = reference;
                    return true;
                case IUISignal signal:
                    target = BindTarget.ForDelegates(() => StateAddress.Infer(signal.Value), v =>
                    {
                        signal.Value = v.AsString();
                        return null;
                    }, $"args.{key} (signal)");
                    return true;
                case Func<double> number when values.TryGetRaw(key + ".set", out object? setter) && setter is Action<double> setNumber:
                    target = BindTarget.ForDelegates(() => DataValue.FromNumber(Call(() => number(), 0d)), v =>
                    {
                        setNumber(v.AsNumber());
                        return null;
                    }, $"args.{key}");
                    return true;
                case Func<string> text when values.TryGetRaw(key + ".set", out object? setter) && setter is Action<string> setText:
                    target = BindTarget.ForDelegates(() => DataValue.FromString(Call(() => text(), string.Empty)), v =>
                    {
                        setText(v.AsString());
                        return null;
                    }, $"args.{key}");
                    return true;
            }

            error = $"args.{key} of '{Name}' is not a two-way value (pass a state key such as \"menu.amount\"; from C#, a signal, or a getter with its setter stored at '{key}.set').";
            return false;
        }

        private ParamDefinition? Param(string key)
        {
            if (parameters == null)
            {
                return null;
            }

            if (parameters.TryGetValue(key, out ParamDefinition? exact))
            {
                return exact;
            }

            return parameters.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        /// <summary>A stored argument as a data value (C# getters are called, so they are volatile).</summary>
        private static DataValue ToValue(object? raw, out bool isVolatile)
        {
            isVolatile = false;
            switch (raw)
            {
                case null:
                    return DataValue.Null;
                case DataArgument argument:
                    isVolatile = argument.IsDynamic;
                    return argument.Current();
                case string text:
                    return DataValue.FromString(text);
                case double number:
                    return DataValue.FromNumber(number);
                case bool flag:
                    return DataValue.FromBool(flag);
                case Func<string> getter:
                    isVolatile = true;
                    return DataValue.FromString(Call(() => getter(), string.Empty));
                case Func<double> numberGetter:
                    isVolatile = true;
                    return DataValue.FromNumber(Call(() => numberGetter(), 0d));
                case IUISignal signal:
                    isVolatile = true;
                    return StateAddress.Infer(Call(() => signal.Value, string.Empty));
                case Delegate:
                    return DataValue.Null;
                default:
                    isVolatile = true;
                    return ModelAccessor.ToValue(raw);
            }
        }

        /// <summary>Call a C# getter; an exception reads as <paramref name="fallback"/> (logged).</summary>
        private static T Call<T>(Func<T> getter, T fallback)
        {
            try
            {
                return getter() ?? fallback;
            }
            catch (Exception ex)
            {
                UIServices.Log($"A composite argument getter failed: {ex.Message}", LogLevel.Trace);
                return fallback;
            }
        }

        /// <summary>Convert to the parameter's type (string / number / bool; anything else as is).</summary>
        internal static DataValue Coerce(DataValue value, string? type)
        {
            if (value.IsNull)
            {
                return value;
            }

            return type?.Trim().ToLowerInvariant() switch
            {
                "number" => value.Kind == DataKind.Number ? value : DataValue.FromNumber(value.AsNumber()),
                "bool" => value.Kind == DataKind.Bool ? value : DataValue.FromBool(value.AsBool()),
                "string" => value.Kind == DataKind.String ? value : DataValue.FromString(value.AsString()),
                _ => value
            };
        }

        public override string ToString() => $"args of {Name}";
    }
}
