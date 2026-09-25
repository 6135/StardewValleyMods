using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Model;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// Thrown when an action entry fails. Event handlers run inside the owner's <see cref="Core.ConsumerContext"/>
    /// guard, which logs it once against the owner and mutes that element's event, exactly like a faulting C# callback.
    /// </summary>
    internal sealed class DataActionException : Exception
    {
        internal DataActionException(string message) : base(message) { }

        // the guard logs ToString(); a data error has no useful stack trace
        public override string ToString() => Message;
    }

    /// <summary>
    /// Runs action lists (event handlers written as data). Each entry's <c>Condition</c> (game state query) and
    /// <c>When</c> gate it; its <c>Action</c> is interpolated through <see cref="Resolver"/> right before it runs and
    /// executed with <see cref="TriggerActionManager.TryRunAction(CachedAction, TriggerActionContext, out string, out Exception)"/>,
    /// so vanilla actions (<c>AddMoney</c>, <c>AddMail</c>...) and other mods' actions work unchanged. The UI scope is
    /// passed in <see cref="TriggerActionContext.CustomFields"/> under <see cref="ScopeField"/> and as an ambient
    /// (thread-static) scope, so it also reaches actions nested in vanilla <c>If</c>.
    /// </summary>
    internal static class DataActionRunner
    {
        /// <summary>Custom field holding the <see cref="DataScope"/> of the action.</summary>
        internal const string ScopeField = "6135.UIFramework/Scope";

        /// <summary>Trigger name reported to actions run by the data layer.</summary>
        internal const string Trigger = "6135.UIFramework_UI";

        [ThreadStatic]
        private static DataScope? ambient;

        /// <summary>The value resolver used for <c>When</c> and action interpolation (the expression resolver from v1.4).</summary>
        internal static ExpressionValueResolver Resolver => ExpressionValueResolver.Instance;

        /// <summary>The scope of the data action running on this thread, if any.</summary>
        internal static DataScope? Ambient => ambient;

        /// <summary>The scope an action runs in: its context's custom field, else the ambient scope.</summary>
        internal static DataScope? ScopeOf(TriggerActionContext context)
        {
            if (context.CustomFields != null && context.CustomFields.TryGetValue(ScopeField, out object? value) && value is DataScope scope)
            {
                return scope;
            }

            return ambient;
        }

        /// <summary>Run an action list; throws <see cref="DataActionException"/> after the whole list ran if an entry failed.</summary>
        internal static void Run(IReadOnlyList<ActionDefinition>? actions, DataScope scope)
        {
            if (actions == null || actions.Count == 0)
            {
                return;
            }

            var errors = new List<string>();
            DataScope? previous = ambient;
            ambient = scope;
            try
            {
                RunList(actions, scope, errors);
            }
            finally
            {
                ambient = previous;
            }

            if (errors.Count > 0)
            {
                throw new DataActionException(string.Join("; ", errors));
            }
        }

        /// <summary>A handler for an element / menu event that runs <paramref name="actions"/> in the event's scope (null when there are none).</summary>
        internal static Action<T>? Handler<T>(IReadOnlyList<ActionDefinition>? actions, DataScope scope, string eventName)
        {
            if (actions == null || actions.Count == 0)
            {
                return null;
            }

            return args => Run(actions, scope.WithEvent(eventName, args));
        }

        /// <summary>Run <paramref name="actions"/> in <paramref name="scope"/> inside the owner's callback guard (errors are logged and mute the entry).</summary>
        internal static void RunGuarded(IReadOnlyList<ActionDefinition>? actions, DataScope scope, string eventName)
        {
            if (actions == null || actions.Count == 0)
            {
                return;
            }

            scope.Consumer.Invoke(scope.GuardId, eventName, () => Run(actions, scope));
        }

        /// <summary>Evaluate a game state query for the current player; an empty query matches.</summary>
        internal static bool CheckCondition(string? condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            return GameStateQuery.CheckConditions(condition, Game1.currentLocation, Game1.player);
        }

        private static void RunList(IReadOnlyList<ActionDefinition> actions, DataScope scope, List<string> errors)
        {
            foreach (ActionDefinition entry in actions)
            {
                if (entry == null)
                {
                    continue;
                }

                if (!Passes(entry, scope))
                {
                    if (entry.Else != null)
                    {
                        RunList(entry.Else, scope, errors);
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.Action))
                {
                    RunOne(entry.Action, scope, errors);
                }

                if (entry.Actions != null)
                {
                    RunList(entry.Actions, scope, errors);
                }
            }
        }

        private static bool Passes(ActionDefinition entry, DataScope scope)
        {
            if (!CheckCondition(entry.Condition))
            {
                return false;
            }

            if (entry.When == null)
            {
                return true;
            }

            DataValue when = Resolver.Evaluate(entry.When, scope, out string? error);
            if (error != null)
            {
                throw new DataActionException($"When '{entry.When}' failed: {error}");
            }

            return when.AsBool();
        }

        /// <summary>
        /// Run one action string in <paramref name="scope"/> (interpolated first; <c>@owner/command args</c> runs a C#
        /// command). False with the error when it failed.
        /// </summary>
        internal static bool RunSingle(string action, DataScope scope, out string error)
        {
            var errors = new List<string>();
            DataScope? previous = ambient;
            ambient = scope;
            try
            {
                RunOne(action, scope, errors);
            }
            catch (DataActionException ex)
            {
                errors.Add(ex.Message);
            }
            finally
            {
                ambient = previous;
            }

            error = string.Join("; ", errors);
            return errors.Count == 0;
        }

        /// <summary>The <c>@owner/command args</c> shorthand as the trigger action it stands for (<c>6135.UIFramework_Invoke owner/command args</c>).</summary>
        internal static string ExpandShorthand(string action)
        {
            return action.StartsWith('@') && action.Length > 1 ? BridgeActions.Invoke + " " + action.Substring(1) : action;
        }

        private static void RunOne(string raw, DataScope scope, List<string> errors)
        {
            string action = ExpandShorthand(Resolver.Interpolate(raw, scope).Trim());
            if (action.Length == 0)
            {
                return;
            }

            CachedAction parsed = TriggerActionManager.ParseAction(action);
            var context = new TriggerActionContext(Trigger, Array.Empty<object>(), null, new Dictionary<string, object> { [ScopeField] = scope });
            if (!TriggerActionManager.TryRunAction(parsed, context, out string? error, out Exception? exception))
            {
                string message = $"action '{action}' failed: {error ?? exception?.Message ?? "unknown error"}";
                errors.Add(message);
                if (exception != null)
                {
                    Core.UIServices.Log($"[{scope.Owner}] {message}\n{exception}", LogLevel.Trace);
                }
            }
        }
    }
}
