using System;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// What a C# hook (command, draw hook) receives from data: its name and arguments, where the call came from
    /// (owner, menu, element, row) and access to the caller's scope (expressions, state and model values).
    /// </summary>
    internal sealed class DataCall : IUIDataCall
    {
        private readonly DataScope? scope;
        private readonly string fallbackOwner;

        internal DataCall(string name, string[] args, DataScope? scope, string fallbackOwner)
        {
            Name = name;
            Args = args ?? Array.Empty<string>();
            this.scope = scope;
            this.fallbackOwner = fallbackOwner;
        }

        public string Name { get; }

        public string[] Args { get; }

        public string OwnerModId => scope?.Owner ?? fallbackOwner;

        public IUIMenu Menu => scope?.Menu!;

        public IUIElement Element => scope?.Element!;

        public object Row => Local(RowScope.RowName) is { IsNull: false } row ? ModelAccessor.ToClr(row)! : null!;

        public int RowIndex => Local(RowScope.IndexName) is { Kind: DataKind.Number } index ? (int)index.AsNumber() : -1;

        /// <summary>The scope expressions and state keys are resolved in (an owner-level scope outside UIs).</summary>
        private DataScope Scope => scope ?? DataScope.ForOwner(fallbackOwner);

        private DataValue? Local(string name) => scope?.Locals != null && scope.Locals.TryGetValue(name, out DataValue value) ? value : null;

        public string Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            DataValue value = ExpressionValueResolver.Instance.Evaluate(expression, Scope, out string? error);
            if (error != null)
            {
                UIServices.Log($"[{OwnerModId}] {Name}: '{expression}': {error}", LogLevel.Warn);
            }

            return value.AsString();
        }

        public string GetState(string key)
        {
            if (!BindTarget.TryParse(key, Scope, allowBare: true, out BindTarget? target, out string error))
            {
                UIServices.Log($"[{OwnerModId}] {Name}: {error}", LogLevel.Warn);
                return string.Empty;
            }

            return target.Read().AsString();
        }

        public bool SetState(string key, string value)
        {
            if (!BindTarget.TryParse(key, Scope, allowBare: true, out BindTarget? target, out string error)
                || !target.WrittenBy(OwnerModId).Write(State.StateAddress.Infer(value), out error))
            {
                UIServices.Log($"[{OwnerModId}] {Name}: could not write '{key}': {error}", LogLevel.Warn);
                return false;
            }

            return true;
        }

        public override string ToString() => $"{Name}({string.Join(", ", Args)}) from {Scope}";
    }
}
