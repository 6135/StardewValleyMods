using System;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;

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
