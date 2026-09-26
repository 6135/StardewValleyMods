using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// What one menu's owner shares with contributors (architecture.md §16.1): string / number / bool getters,
    /// commands and event subscriptions, keyed by name. Owner delegates run through the owner's callback guard;
    /// subscriber handlers run through the subscriber's. Keyed by (owner, menu id), so exposures survive the menu
    /// object being recreated.
    /// </summary>
    internal sealed class ScreenExposures
    {
        private readonly Dictionary<string, Func<string>> strings = new();
        private readonly Dictionary<string, Func<double>> numbers = new();
        private readonly Dictionary<string, Func<bool>> bools = new();
        private readonly Dictionary<string, Action> commands = new();
        private readonly Dictionary<string, List<Subscription>> subscriptions = new();

        /// <summary>One subscriber's handler for an event.</summary>
        private sealed record Subscription(ConsumerContext Subscriber, Action Handler);

        internal ScreenExposures(ConsumerContext owner, string menuId)
        {
            Owner = owner;
            MenuId = menuId;
        }

        internal ConsumerContext Owner { get; }
        internal string MenuId { get; }

        /// <summary>Guard id used in log lines for the owner's delegates.</summary>
        private string GuardId => MenuId + ".exposed";

        // ---------------------------------------------------------------------------------------------------------
        //  Owner side
        // ---------------------------------------------------------------------------------------------------------

        internal void SetString(string key, Func<string>? value) => Set(strings, key, value);
        internal void SetNumber(string key, Func<double>? value) => Set(numbers, key, value);
        internal void SetBool(string key, Func<bool>? value) => Set(bools, key, value);
        internal void SetCommand(string key, Action? command) => Set(commands, key, command);

        private static void Set<T>(Dictionary<string, T> table, string key, T? value) where T : class
        {
            if (value == null)
            {
                table.Remove(key);
            }
            else
            {
                table[key] = value;
            }
        }

        /// <summary>Raise <paramref name="eventName"/> to every subscriber, each through its own guard.</summary>
        internal void Publish(string eventName)
        {
            if (!subscriptions.TryGetValue(eventName, out List<Subscription>? list))
            {
                return;
            }

            foreach (Subscription s in list.ToArray())
            {
                s.Subscriber.Invoke(Owner.ModId + "." + MenuId, "event:" + eventName, s.Handler);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Contributor side
        // ---------------------------------------------------------------------------------------------------------

        internal string[] Keys => strings.Keys.Concat(numbers.Keys).Concat(bools.Keys).Distinct().ToArray();

        internal bool HasValue(string key) => strings.ContainsKey(key) || numbers.ContainsKey(key) || bools.ContainsKey(key);

        internal bool HasCommand(string key) => commands.ContainsKey(key);

        internal string GetString(string key)
        {
            if (strings.TryGetValue(key, out Func<string>? s))
            {
                return Owner.Invoke(GuardId, key, s, string.Empty) ?? string.Empty;
            }

            if (numbers.TryGetValue(key, out Func<double>? n))
            {
                return Owner.Invoke(GuardId, key, n, 0d).ToString(CultureInfo.InvariantCulture);
            }

            return bools.TryGetValue(key, out Func<bool>? b) && Owner.Invoke(GuardId, key, b, false) ? "true" : string.Empty;
        }

        internal double GetNumber(string key)
        {
            if (numbers.TryGetValue(key, out Func<double>? n))
            {
                return Owner.Invoke(GuardId, key, n, 0d);
            }

            if (bools.TryGetValue(key, out Func<bool>? b))
            {
                return Owner.Invoke(GuardId, key, b, false) ? 1 : 0;
            }

            if (strings.TryGetValue(key, out Func<string>? s) && double.TryParse(Owner.Invoke(GuardId, key, s, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                return parsed;
            }

            return 0;
        }

        internal bool GetBool(string key)
        {
            if (bools.TryGetValue(key, out Func<bool>? b))
            {
                return Owner.Invoke(GuardId, key, b, false);
            }

            if (numbers.TryGetValue(key, out Func<double>? n))
            {
                return !Numbers.Same(Owner.Invoke(GuardId, key, n, 0d), 0);
            }

            return strings.TryGetValue(key, out Func<string>? s) && bool.TryParse(Owner.Invoke(GuardId, key, s, string.Empty), out bool parsed) && parsed;
        }

        internal void Invoke(string command)
        {
            if (commands.TryGetValue(command, out Action? action))
            {
                Owner.Invoke(GuardId, "command:" + command, action);
            }
        }

        internal void Subscribe(ConsumerContext subscriber, string eventName, Action handler)
        {
            if (!subscriptions.TryGetValue(eventName, out List<Subscription>? list))
            {
                subscriptions[eventName] = list = new List<Subscription>();
            }

            list.Add(new Subscription(subscriber, handler));
        }

        internal void Unsubscribe(ConsumerContext subscriber, string eventName, Action handler)
        {
            if (subscriptions.TryGetValue(eventName, out List<Subscription>? list))
            {
                list.RemoveAll(s => s.Subscriber.ModId == subscriber.ModId && s.Handler == handler);
            }
        }

        /// <summary>
        /// Drop every subscription of <paramref name="subscriber"/>: its contributions and decorators are about to run
        /// again (the menu opens or is rebuilt) and subscribe anew, or it no longer extends the menu.
        /// </summary>
        internal void RemoveSubscriptions(ConsumerContext subscriber)
        {
            foreach (List<Subscription> list in subscriptions.Values)
            {
                list.RemoveAll(s => s.Subscriber.ModId == subscriber.ModId);
            }
        }
    }
}
