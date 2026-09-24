using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// Listens to <see cref="IInputEvents.ButtonsChanged"/> once and dispatches to registered keybind lists.
    /// Keys are namespaced by consumer id.
    /// </summary>
    internal sealed class HotkeyService
    {
        private sealed class Binding
        {
            internal KeybindList Keys = new();
            internal Action OnPressed = () => { };
            internal ConsumerContext Consumer = ConsumerContext.None;
            internal string Id = string.Empty;
        }

        private readonly Dictionary<string, Binding> bindings = new();

        internal HotkeyService(IModEvents events)
        {
            events.Input.ButtonsChanged += OnButtonsChanged;
        }

        /// <summary>Number of registered bindings (all consumers).</summary>
        internal int Count => bindings.Count;

        /// <summary>Parse a keybind list string; returns false (and logs) if it is invalid.</summary>
        internal static bool TryParse(string? keybindList, ConsumerContext consumer, out KeybindList keys)
        {
            keys = new KeybindList();
            if (string.IsNullOrWhiteSpace(keybindList))
            {
                return false;
            }

            if (!KeybindList.TryParse(keybindList, out KeybindList? parsed, out string[] errors))
            {
                UIServices.Log($"[{consumer.ModId}] invalid keybind list '{keybindList}': {string.Join("; ", errors)}", LogLevel.Warn);
                return false;
            }
            keys = parsed;
            return keys.IsBound;
        }

        internal void Register(ConsumerContext consumer, string id, string keybindList, Action onPressed)
        {
            string key = consumer.ModId + "|" + id;
            if (!TryParse(keybindList, consumer, out KeybindList keys))
            {
                bindings.Remove(key);
                return;
            }
            bindings[key] = new Binding { Keys = keys, OnPressed = onPressed, Consumer = consumer, Id = id };
        }

        internal void Unregister(ConsumerContext consumer, string id) => bindings.Remove(consumer.ModId + "|" + id);

        internal void UnregisterAll(ConsumerContext consumer)
        {
            string prefix = consumer.ModId + "|";
            foreach (string key in new List<string>(bindings.Keys))
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    bindings.Remove(key);
                }
            }
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (bindings.Count == 0)
            {
                return;
            }

            foreach (Binding binding in new List<Binding>(bindings.Values))
            {
                if (binding.Keys.JustPressed())
                {
                    binding.Consumer.Invoke(binding.Id, "Hotkey", binding.OnPressed);
                }
            }
        }
    }
}
