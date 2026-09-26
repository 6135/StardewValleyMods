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
        private string? writerOwner;

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
            target.writerOwner = scope.Owner;
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

        /// <summary>Judge state writes as made by <paramref name="owner"/> (see <see cref="DataStateStore.CanWrite"/>) instead of the parsing scope's owner.</summary>
        internal BindTarget WrittenBy(string owner)
        {
            writerOwner = owner;
            return this;
        }

        /// <summary>Write a value; false (with an error) when the target cannot hold it (or may not be written by the owner that parsed it).</summary>
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

                return DataStateStore.Active.CanWrite(address, writerOwner, out error) && DataStateStore.Active.Write(address, value, out error);
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
