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
                || !target.Write(State.StateAddress.Infer(value), out error))
            {
                UIServices.Log($"[{OwnerModId}] {Name}: could not write '{key}': {error}", LogLevel.Warn);
                return false;
            }

            return true;
        }

        public override string ToString() => $"{Name}({string.Join(", ", Args)}) from {Scope}";
    }
}

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// <c>IStardewUIApi.DataState</c>: a live <see cref="IUISignal"/> view of one data state value on the current screen.
    /// Reads and writes go through the <see cref="State.DataStateStore"/> (so watches and bindings react); subscribers
    /// run after the value changed on any screen.
    /// </summary>
    internal sealed class DataStateSignal : IUISignal
    {
        private readonly State.DataStateStore store;
        private readonly State.StateAddress address;
        private readonly System.Collections.Generic.List<Action> handlers = new();
        private bool listening;

        internal DataStateSignal(State.DataStateStore store, State.StateAddress address)
        {
            this.store = store;
            this.address = address;
        }

        public string Value
        {
            get => store.Read(address).AsString();
            set => Write(State.StateAddress.Infer(value));
        }

        public double Number
        {
            get => store.Read(address).AsNumber();
            set => Write(DataValue.FromNumber(value));
        }

        public bool Flag
        {
            get => store.Read(address).AsBool();
            set => Write(DataValue.FromBool(value));
        }

        public int Version { get; private set; }

        public void Subscribe(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            handlers.Add(handler);
            if (!listening)
            {
                listening = true;
                store.Changed += OnChanged;
            }
        }

        public void Unsubscribe(Action handler)
        {
            handlers.Remove(handler);
            if (handlers.Count == 0 && listening)
            {
                listening = false;
                store.Changed -= OnChanged;
            }
        }

        private void Write(DataValue value)
        {
            if (!store.Write(address, value, out string error))
            {
                UIServices.Log($"DataState {address}: {error}", LogLevel.Warn);
            }
        }

        private void OnChanged(int screen, State.StateAddress changed, DataValue oldValue, DataValue newValue)
        {
            if (changed.Scope != address.Scope
                || !string.Equals(changed.Container, address.Container, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(changed.Name, address.Name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Version++;
            foreach (Action handler in handlers.ToArray())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    UIServices.Log($"A DataState subscriber of {address} failed: {ex}", LogLevel.Error);
                }
            }
        }

        public override string ToString() => address.ToString();
    }
}
