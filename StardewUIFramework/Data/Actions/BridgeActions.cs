using System;
using StardewValley.Delegates;
using StardewValley.Triggers;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.State;
using UIFramework.Hosting;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The v1.6 bridge action <c>6135.UIFramework_Invoke &lt;target&gt; [args...]</c> (also written <c>@target args</c>
    /// in data action lists and <c>RunAction</c>). The target resolves in this order:
    /// <list type="number">
    ///   <item><c>ctx.&lt;cmd&gt;</c>: a command the menu's owner exposed from C# (<c>ExposeCommand</c>);</item>
    ///   <item><c>#&lt;elementId&gt;.&lt;cmd&gt;</c>: a command of a composite instance in the menu (<c>ExposeCommand</c> on its host);</item>
    ///   <item><c>&lt;ModId&gt;/&lt;cmd&gt;</c>: a command registered with <c>RegisterCommand</c>;</item>
    ///   <item><c>&lt;cmd&gt;</c>: the menu's exposed command of that name, else the scope owner's registered command.</item>
    /// </list>
    /// The menu is the one the action runs in, else the topmost open framework menu.
    /// <para>
    /// v1.7 adds <c>6135.UIFramework_Publish &lt;event&gt; [owner/menu]</c>: inside a data composite's body it raises the
    /// composite's event (<c>"On"</c> on the instance, <c>IUIComposite.Subscribe</c> in C#); elsewhere it raises the
    /// menu's event to its contributors (<c>"On"</c> in <c>Contributions</c>, <c>IUIScreenContext.Subscribe</c>). Only the
    /// menu's owner may publish its events.
    /// </para>
    /// </summary>
    internal static class BridgeActions
    {
        internal const string Invoke = FrameworkTriggerActions.Prefix + "Invoke";
        internal const string Publish = FrameworkTriggerActions.Prefix + "Publish";

        /// <summary>Register the action (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            TriggerActionManager.RegisterAction(Invoke, (string[] args, TriggerActionContext context, out string error) =>
            {
                string? failure = Run(data, args, DataActionRunner.ScopeOf(context));
                error = failure!; // null on success: the game treats any non-null error as a failure
                return failure == null;
            });
            TriggerActionManager.RegisterAction(Publish, (string[] args, TriggerActionContext context, out string error) =>
            {
                string? failure = RunPublish(data, args, DataActionRunner.ScopeOf(context));
                error = failure!; // null on success: the game treats any non-null error as a failure
                return failure == null;
            });
        }

        private static string? RunPublish(DataService data, string[] args, DataScope? scope)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                return "Publish needs an event name: Publish <event> [owner/menu].";
            }

            string eventName = args[1].Trim();
            string? key = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) ? args[2].Trim() : null;

            // in a data composite's body: the composite's event
            if (key == null && Building.TemplateArgs.Of(scope)?.Host is { } host)
            {
                host.Publish(eventName);
                return null;
            }

            UIMenu? menu = key != null ? data.Find(key) : scope?.Menu;
            if (menu == null)
            {
                return key != null ? $"no menu '{key}' is registered." : "Publish needs a menu: run it in a menu or a data composite, or pass <owner/menu>.";
            }

            if (scope != null && scope.Owner.Length > 0 && !string.Equals(scope.Owner, menu.Consumer.ModId, StringComparison.OrdinalIgnoreCase))
            {
                return $"only {menu.Consumer.ModId} can publish the events of its menu '{menu.Id}'.";
            }

            ScreenExposures? exposures = ScopeRoots.Exposures?.Invoke(menu);
            if (exposures == null)
            {
                return "menu events are not available.";
            }

            exposures.Publish(eventName);
            return null;
        }

        private static string? Run(DataService data, string[] args, DataScope? scope)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            {
                return "Invoke needs a target: ctx.<cmd>, #<elementId>.<cmd>, <ModId>/<cmd> or <cmd>.";
            }

            string target = args[1].Trim();
            string[] rest = args.Length > 2 ? args[2..] : Array.Empty<string>();
            UIMenu? menu = scope?.Menu ?? data.Topmost;

            // ctx.<cmd>
            if (target.StartsWith("ctx.", StringComparison.Ordinal))
            {
                string command = target.Substring(4);
                ScreenExposures? exposures = menu != null ? ScopeRoots.Exposures?.Invoke(menu) : null;
                if (exposures == null || !exposures.HasCommand(command))
                {
                    return $"the menu{(menu != null ? $" '{menu.Id}'" : string.Empty)} exposes no command '{command}'.";
                }

                exposures.Invoke(command);
                HookRegistry.BumpData();
                return null;
            }

            // #<elementId>.<cmd>
            if (target.StartsWith('#'))
            {
                int dot = target.LastIndexOf('.');
                if (dot <= 1 || dot == target.Length - 1)
                {
                    return $"'{target}' is not #<elementId>.<command>.";
                }

                string id = target.Substring(1, dot - 1);
                string command = target.Substring(dot + 1);
                if (menu?.Root.FindById(id) is not IUIComposite composite)
                {
                    return $"no composite '{id}' in {(menu != null ? $"menu '{menu.Id}'" : "an open menu")}.";
                }

                if (!composite.HasCommand(command))
                {
                    return $"composite '{id}' ({composite.CompositeName}) exposes no command '{command}'.";
                }

                composite.Invoke(command);
                HookRegistry.BumpData();
                return null;
            }

            HookRegistry? hooks = UIServices.Hooks;
            string owner = scope?.Owner ?? menu?.Consumer.ModId ?? string.Empty;

            // <cmd>: the menu's exposed command first
            if (!target.Contains('/') && menu != null && ScopeRoots.Exposures?.Invoke(menu) is { } menuExposures && menuExposures.HasCommand(target))
            {
                menuExposures.Invoke(target);
                HookRegistry.BumpData();
                return null;
            }

            // <ModId>/<cmd>, or the owner's <cmd>
            if (hooks == null)
            {
                return "C# commands are not available.";
            }

            return hooks.RunCommand(HookRegistry.Qualify(target, owner), rest, scope, owner, out string error) ? null : error;
        }
    }
}
