using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Rendering;

namespace UIFramework.Core
{
    /// <summary>
    /// Per-consumer state (one per mod that requested the API): identity for logging, tooltip delay / default style
    /// overrides, and the callback guard that keeps a faulting consumer callback from crashing the game loop.
    /// </summary>
    internal sealed class ConsumerContext
    {
        /// <summary>Context used for elements that are not (yet) attached to a menu.</summary>
        internal static readonly ConsumerContext None = new("(none)");

        /// <summary>Callbacks that threw, by (menu id, element id, event); the menu id is empty when the caller did not give one.</summary>
        private readonly HashSet<(string Menu, string Element, string Event)> muted = new();

        internal string ModId { get; }

        /// <summary>Tooltip delay override for this consumer (null = framework config).</summary>
        internal int? TooltipDelayMs { get; set; }

        /// <summary>Default style for this consumer's elements (null = theme default).</summary>
        internal UIStyle? DefaultStyle { get; set; }

        /// <summary>Signal bindings this consumer created (SIGNALS; dropped per element on detach / unbind).</summary>
        internal SignalBindings Bindings { get; } = new();

        internal ConsumerContext(string modId)
        {
            ModId = modId;
        }

        internal int EffectiveTooltipDelay => TooltipDelayMs ?? UIServices.Config.TooltipDelayMs;

        /// <summary>Run a consumer callback outside any menu (or whose caller does not know the menu); see <see cref="Invoke(string, string, string, Action)"/>.</summary>
        internal void Invoke(string elementId, string eventName, Action? action) => Invoke(string.Empty, elementId, eventName, action);

        /// <summary>
        /// Run a consumer callback of an element of menu <paramref name="menuId"/>. Exceptions are logged once per
        /// (menu, element, event) and the callback is then muted.
        /// </summary>
        internal void Invoke(string menuId, string elementId, string eventName, Action? action)
        {
            if (action == null || IsMuted(menuId, elementId, eventName))
            {
                return;
            }

            LogCall(elementId, eventName);
            long started = PerfCounters.StartCallback();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Mute(menuId, elementId, eventName, ex);
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        /// <summary>
        /// <see cref="Invoke(string, string, string, Action)"/> for a callback that needs arguments: pass them in
        /// <paramref name="state"/> and use a static lambda, so the call allocates no closure (per-frame paths).
        /// </summary>
        internal void InvokeWith<TState>(string menuId, string elementId, string eventName, Action<TState> action, TState state)
        {
            if (IsMuted(menuId, elementId, eventName))
            {
                return;
            }

            LogCall(elementId, eventName);
            long started = PerfCounters.StartCallback();
            try
            {
                action(state);
            }
            catch (Exception ex)
            {
                Mute(menuId, elementId, eventName, ex);
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        /// <summary><see cref="InvokeWith{TState}"/> for a callback that returns a value; <paramref name="fallback"/> is returned if it faults or is muted.</summary>
        internal TResult InvokeWith<TState, TResult>(string menuId, string elementId, string eventName, Func<TState, TResult> func, TState state, TResult fallback)
        {
            if (IsMuted(menuId, elementId, eventName))
            {
                return fallback;
            }

            long started = PerfCounters.StartCallback();
            try
            {
                return func(state);
            }
            catch (Exception ex)
            {
                Mute(menuId, elementId, eventName, ex);
                return fallback;
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        /// <summary>Run a consumer callback that returns a value, outside any menu; see <see cref="Invoke{T}(string, string, string, Func{T}, T)"/>.</summary>
        internal T Invoke<T>(string elementId, string eventName, Func<T>? func, T fallback) => Invoke(string.Empty, elementId, eventName, func, fallback);

        /// <summary>Run a consumer callback that returns a value; <paramref name="fallback"/> is returned if it faults or is muted.</summary>
        internal T Invoke<T>(string menuId, string elementId, string eventName, Func<T>? func, T fallback)
        {
            if (func == null || IsMuted(menuId, elementId, eventName))
            {
                return fallback;
            }

            long started = PerfCounters.StartCallback();
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Mute(menuId, elementId, eventName, ex);
                return fallback;
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        private bool IsMuted(string menuId, string elementId, string eventName) => muted.Count > 0 && muted.Contains((menuId, elementId, eventName));

        /// <summary><see cref="ModConfig.LogCallbacks"/>: log an event callback (value getters, which run every frame, are not logged).</summary>
        private void LogCall(string elementId, string eventName)
        {
            if (UIServices.Config.LogCallbacks)
            {
                UIServices.Log($"[{ModId}] {eventName} on '{elementId}'");
            }
        }

        private void Mute(string menuId, string elementId, string eventName, Exception ex)
        {
            muted.Add((menuId, elementId, eventName));
            string where = menuId.Length == 0 ? string.Empty : $" of menu '{menuId}'";
            UIServices.Log($"[{ModId}] callback '{eventName}' on element '{elementId}'{where} threw an exception and has been muted:\n{ex}", LogLevel.Error);
        }

        /// <summary>Forget the muted callbacks of menu <paramref name="menuId"/> and those muted without a menu (the consumer rebuilt the menu).</summary>
        internal void ResetMutes(string menuId) => muted.RemoveWhere(k => k.Menu.Length == 0 || k.Menu == menuId);
    }
}
