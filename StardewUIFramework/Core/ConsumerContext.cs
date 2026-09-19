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

        private readonly HashSet<string> muted = new();

        internal string ModId { get; }

        /// <summary>Tooltip delay override for this consumer (null = framework config).</summary>
        internal int? TooltipDelayMs { get; set; }

        /// <summary>Default style for this consumer's elements (null = theme default).</summary>
        internal UIStyle? DefaultStyle { get; set; }

        internal ConsumerContext(string modId)
        {
            ModId = modId;
        }

        internal int EffectiveTooltipDelay => TooltipDelayMs ?? UIServices.Config.TooltipDelayMs;

        /// <summary>Run a consumer callback. Exceptions are logged once per (element, event) and the callback is then muted.</summary>
        internal void Invoke(string elementId, string eventName, Action? action)
        {
            if (action == null)
            {
                return;
            }

            string key = elementId + "|" + eventName;
            if (muted.Contains(key))
            {
                return;
            }

            if (UIServices.Config.LogCallbacks)
            {
                UIServices.Log($"[{ModId}] {eventName} on '{elementId}'");
            }

            long started = PerfCounters.StartCallback();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Mute(key, elementId, eventName, ex);
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        /// <summary>Run a consumer callback that returns a value; <paramref name="fallback"/> is returned if it faults or is muted.</summary>
        internal T Invoke<T>(string elementId, string eventName, Func<T>? func, T fallback)
        {
            if (func == null)
            {
                return fallback;
            }

            string key = elementId + "|" + eventName;
            if (muted.Contains(key))
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
                Mute(key, elementId, eventName, ex);
                return fallback;
            }
            finally
            {
                PerfCounters.EndCallback(started);
            }
        }

        private void Mute(string key, string elementId, string eventName, Exception ex)
        {
            muted.Add(key);
            UIServices.Log($"[{ModId}] callback '{eventName}' on element '{elementId}' threw an exception and has been muted:\n{ex}", LogLevel.Error);
        }

        /// <summary>Forget muted callbacks (e.g. when the consumer rebuilds a menu).</summary>
        internal void ResetMutes() => muted.Clear();
    }
}
