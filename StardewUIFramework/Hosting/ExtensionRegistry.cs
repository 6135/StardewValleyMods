using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The cross-mod extension service (architecture.md §16.1): slot contributions, screen decorators and exposed
    /// values / commands / events, all keyed by (owner mod id, menu id[, slot id]) so registrations can precede the
    /// owner's menu. Rebuilds every slot and runs the decorators each time a menu opens, after the owner's tree
    /// exists and before layout (<see cref="MenuRegistry.MenuOpening"/>).
    /// <para>
    /// Everything here is process-wide on purpose: menu models are shared across split-screen players (only their
    /// host and view state are per screen, in <see cref="UIMenu"/>), and the registrations are mod-level.
    /// Per-player values stay in the owners' delegates, which are read live.
    /// </para>
    /// </summary>
    internal sealed class ExtensionRegistry
    {
        private sealed record Contribution(ConsumerContext Contributor, int Priority, Action<IUIContainer, IUIScreenContext> Build);

        private sealed record Decorator(ConsumerContext Mod, Action<IUIMenu> Decorate);

        private readonly MenuRegistry menus;
        private readonly Dictionary<string, List<Contribution>> contributions = new();
        private readonly Dictionary<string, List<Decorator>> decorators = new();
        private readonly Dictionary<string, ScreenExposures> exposures = new();

        internal ExtensionRegistry(MenuRegistry menus)
        {
            this.menus = menus;
            menus.MenuOpening += Rebuild;
        }

        private static string MenuKey(string ownerModId, string menuId) => ownerModId + "|" + menuId;

        private static string SlotKey(string ownerModId, string menuId, string slotId) => MenuKey(ownerModId, menuId) + "|" + slotId;

        // ---------------------------------------------------------------------------------------------------------
        //  Registration
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Register (or replace) <paramref name="contributor"/>'s contribution to a slot.</summary>
        internal void Contribute(ConsumerContext contributor, string ownerModId, string menuId, string slotId, int priority, Action<IUIContainer, IUIScreenContext> build)
        {
            string key = SlotKey(ownerModId, menuId, slotId);
            if (!contributions.TryGetValue(key, out List<Contribution>? list))
            {
                contributions[key] = list = new List<Contribution>();
            }

            list.RemoveAll(c => c.Contributor.ModId == contributor.ModId);
            list.Add(new Contribution(contributor, priority, build));
        }

        internal void RemoveContribution(ConsumerContext contributor, string ownerModId, string menuId, string slotId)
        {
            if (contributions.TryGetValue(SlotKey(ownerModId, menuId, slotId), out List<Contribution>? list))
            {
                list.RemoveAll(c => c.Contributor.ModId == contributor.ModId);
            }

            DropSubscriptionsIfGone(contributor, ownerModId, menuId);
        }

        /// <summary>Register (or replace; null removes) <paramref name="mod"/>'s decorator for a menu.</summary>
        internal void SetDecorator(ConsumerContext mod, string ownerModId, string menuId, Action<IUIMenu>? decorate)
        {
            string key = MenuKey(ownerModId, menuId);
            if (!decorators.TryGetValue(key, out List<Decorator>? list))
            {
                decorators[key] = list = new List<Decorator>();
            }

            list.RemoveAll(d => d.Mod.ModId == mod.ModId);
            if (decorate != null)
            {
                list.Add(new Decorator(mod, decorate));
            }
            else
            {
                DropSubscriptionsIfGone(mod, ownerModId, menuId);
            }
        }

        /// <summary>Drop <paramref name="mod"/>'s event subscriptions on a menu once it has neither a contribution nor a decorator there.</summary>
        private void DropSubscriptionsIfGone(ConsumerContext mod, string ownerModId, string menuId)
        {
            string menuKey = MenuKey(ownerModId, menuId);
            if (!exposures.TryGetValue(menuKey, out ScreenExposures? table))
            {
                return;
            }

            bool decorates = decorators.TryGetValue(menuKey, out List<Decorator>? decorating) && decorating.Any(d => d.Mod.ModId == mod.ModId);
            bool contributes = contributions.Any(p => p.Key.StartsWith(menuKey + "|", StringComparison.Ordinal) && p.Value.Any(c => c.Contributor.ModId == mod.ModId));
            if (!decorates && !contributes)
            {
                table.RemoveSubscriptions(mod);
            }
        }

        /// <summary>Whether <paramref name="mod"/> registered a decorator for <paramref name="menu"/> (and may therefore edit its unsealed tree).</summary>
        internal bool IsDecorator(ConsumerContext mod, UIMenu menu)
        {
            return decorators.TryGetValue(MenuKey(menu.Consumer.ModId, menu.Id), out List<Decorator>? list) && list.Any(d => d.Mod.ModId == mod.ModId);
        }

        /// <summary>The exposure table of a menu (created on first use).</summary>
        internal ScreenExposures ExposuresOf(UIMenu menu)
        {
            string key = MenuKey(menu.Consumer.ModId, menu.Id);
            if (!exposures.TryGetValue(key, out ScreenExposures? table))
            {
                exposures[key] = table = new ScreenExposures(menu.Consumer, menu.Id);
            }

            return table;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Discovery / debug
        // ---------------------------------------------------------------------------------------------------------

        private static IEnumerable<Slot> SlotsOf(UIMenu menu) => menu.Root.SelfAndDescendants().OfType<Slot>();

        internal IUISlotInfo[] ListSlots(string ownerModId)
        {
            return menus.MenusOf(ownerModId)
                .SelectMany(menu => SlotsOf(menu).Select(slot => new SlotInfo(ownerModId, menu.Id, slot)))
                .ToArray<IUISlotInfo>();
        }

        /// <summary>The slot map of every open menu, for the <c>ui_slots</c> console command.</summary>
        internal string DescribeOpenMenus()
        {
            if (menus.OpenMenus.Count == 0)
            {
                return "No framework menus are open.";
            }

            var sb = new StringBuilder("Slots of open menus:");
            foreach (UIMenu menu in menus.OpenMenus)
            {
                sb.Append("\n  ").Append(menu);
                foreach (Slot slot in SlotsOf(menu))
                {
                    sb.Append("\n    ").Append(Describe(menu, slot));
                }
            }
            return sb.ToString();
        }

        private string Describe(UIMenu menu, Slot slot)
        {
            var hints = new List<string> { slot.Horizontal ? "row" : "column" };
            if (slot.Horizontal && slot.Wrap)
            {
                hints.Add("wrap");
            }

            if (slot.MaxHeight.HasValue)
            {
                hints.Add("maxHeight=" + slot.MaxHeight.Value);
            }

            if (slot.MaxContributions > 0)
            {
                hints.Add("max=" + slot.MaxContributions);
            }

            if (slot.VetoedContributors.Length > 0)
            {
                hints.Add("vetoed=" + string.Join(",", slot.VetoedContributors));
            }

            string built = string.Join(", ", slot.ContributorIds());
            int registered = contributions.TryGetValue(SlotKey(menu.Consumer.ModId, menu.Id, slot.Id), out List<Contribution>? list) ? list.Count : 0;
            return $"{slot.Id} [{string.Join(", ", hints)}]{(slot.Visible ? string.Empty : " (hidden)")} contributors: {(built.Length == 0 ? "(none)" : built)} ({registered} registered)";
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Rebuild on open
        // ---------------------------------------------------------------------------------------------------------

        private void Rebuild(UIMenu menu)
        {
            Slot[] slots = SlotsOf(menu).ToArray();
            decorators.TryGetValue(MenuKey(menu.Consumer.ModId, menu.Id), out List<Decorator>? list);
            ForgetSubscriptions(menu, slots, list);
            foreach (Slot slot in slots)
            {
                RebuildSlot(menu, slot);
            }

            if (list != null)
            {
                foreach (Decorator d in list.ToArray())
                {
                    RunExternal(menu, d.Mod, menu.Consumer.ModId + "." + menu.Id, "OnScreenBuilt", () => d.Decorate(menu));
                }
            }
        }

        /// <summary>
        /// Before the contributions and decorators of <paramref name="menu"/> run again, drop the event subscriptions
        /// they made last time (they subscribe anew), so a handler never runs once per past open.
        /// </summary>
        private void ForgetSubscriptions(UIMenu menu, Slot[] slots, List<Decorator>? decorating)
        {
            if (!exposures.TryGetValue(MenuKey(menu.Consumer.ModId, menu.Id), out ScreenExposures? table))
            {
                return;
            }

            foreach (Slot slot in slots)
            {
                if (contributions.TryGetValue(SlotKey(menu.Consumer.ModId, menu.Id, slot.Id), out List<Contribution>? contributing))
                {
                    foreach (Contribution c in contributing)
                    {
                        table.RemoveSubscriptions(c.Contributor);
                    }
                }
            }

            if (decorating != null)
            {
                foreach (Decorator d in decorating)
                {
                    table.RemoveSubscriptions(d.Mod);
                }
            }
        }

        /// <summary>Empty the slot and build every accepted contribution into a container of its own, in priority / mod id order.</summary>
        private void RebuildSlot(UIMenu menu, Slot slot)
        {
            slot.Clear();
            string key = SlotKey(menu.Consumer.ModId, menu.Id, slot.Id);
            if (contributions.TryGetValue(key, out List<Contribution>? list))
            {
                int accepted = 0;
                foreach (Contribution c in list.OrderBy(c => c.Priority).ThenBy(c => c.Contributor.ModId, StringComparer.Ordinal).ToArray())
                {
                    if (slot.MaxContributions > 0 && accepted >= slot.MaxContributions)
                    {
                        break;
                    }

                    if (slot.IsVetoed(c.Contributor.ModId))
                    {
                        UIServices.Log($"[{c.Contributor.ModId}] contribution to slot '{slot.Id}' of {menu} was vetoed by the owner.");
                        continue;
                    }

                    BuildContribution(menu, slot, c, key);
                    accepted++;
                }
            }
            slot.ApplyVisibility();
        }

        private void BuildContribution(UIMenu menu, Slot slot, Contribution c, string slotKey)
        {
            var host = new Stack(slot.Id + "." + c.Contributor.ModId, slot.Horizontal, spacing: 8) { Contributor = c.Contributor, Wrap = slot.Wrap };
            slot.Add(host);
            var context = new ScreenContext(ExposuresOf(menu), c.Contributor);
            RunExternal(menu, c.Contributor, slotKey.Replace('|', '.'), "Contribute", () => c.Build(host, context));
        }

        /// <summary>Run a foreign mod's callback against <paramref name="menu"/> through that mod's guard, with sealed subtrees hidden from <see cref="UIMenu.Find"/> meanwhile.</summary>
        private static void RunExternal(UIMenu menu, ConsumerContext mod, string guardId, string eventName, Action action)
        {
            ConsumerContext? previous = menu.ExternalConsumer;
            menu.ExternalConsumer = mod.ModId == menu.Consumer.ModId ? null : mod;
            try
            {
                mod.Invoke(guardId, eventName, action);
            }
            finally
            {
                menu.ExternalConsumer = previous;
            }
        }
    }
}
