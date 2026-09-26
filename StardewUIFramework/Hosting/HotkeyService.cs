using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
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

            /// <summary>The menu a toggle binding opens and closes, as (owner mod id, menu id); null for plain hotkeys.</summary>
            internal (string Owner, string Menu)? Toggles;
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

        internal void Register(ConsumerContext consumer, string id, string keybindList, Action onPressed) => Register(consumer, id, keybindList, onPressed, null);

        /// <summary>
        /// Bind <paramref name="consumer"/>'s toggle hotkey for the menu <paramref name="menuId"/> of <paramref name="ownerModId"/>
        /// (an empty list removes it). <paramref name="toggle"/> resolves the menu when the key is pressed; the binding is
        /// dropped when that menu is destroyed or replaced (<see cref="ForgetMenu"/>).
        /// </summary>
        internal void RegisterToggle(ConsumerContext consumer, string ownerModId, string menuId, string keybindList, Action toggle)
        {
            string id = ToggleId(ownerModId, menuId);
            if (string.IsNullOrWhiteSpace(keybindList))
            {
                Unregister(consumer, id);
                return;
            }

            Register(consumer, id, keybindList, toggle, (ownerModId, menuId));
        }

        private static string ToggleId(string ownerModId, string menuId) => "__toggle:" + ownerModId + "/" + menuId;

        private void Register(ConsumerContext consumer, string id, string keybindList, Action onPressed, (string Owner, string Menu)? toggles)
        {
            string key = consumer.ModId + "|" + id;
            if (!TryParse(keybindList, consumer, out KeybindList keys))
            {
                bindings.Remove(key);
                return;
            }
            bindings[key] = new Binding { Keys = keys, OnPressed = onPressed, Consumer = consumer, Id = id, Toggles = toggles };
        }

        internal void Unregister(ConsumerContext consumer, string id) => bindings.Remove(consumer.ModId + "|" + id);

        /// <summary>Drop every consumer's toggle binding of <paramref name="menu"/> (the menu was destroyed or replaced).</summary>
        internal void ForgetMenu(UIMenu menu)
        {
            foreach ((string key, Binding binding) in new List<KeyValuePair<string, Binding>>(bindings))
            {
                if (binding.Toggles is { } target && target.Owner == menu.Consumer.ModId && target.Menu == menu.Id)
                {
                    bindings.Remove(key);
                }
            }
        }

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
            // no hotkeys while the player types (chat, a vanilla text box or a framework text input)
            if (bindings.Count == 0 || Game1.keyboardDispatcher?.Subscriber != null)
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
