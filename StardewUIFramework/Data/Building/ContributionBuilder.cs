using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Hosting;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The <c>Contributions</c> asset (v1.7): what mods and content packs add to other mods' menus, C# or data, through
    /// the contributor's own API instance:
    /// <list type="bullet">
    ///   <item><c>Slot</c> + <c>Children</c>: one <c>ContributeTo</c> per contributor and slot (a contributor's entries for the same slot share it, in <c>Priority</c> order), rebuilt every time the menu opens;</item>
    ///   <item><c>Decorate</c> and <c>On</c>: one <c>OnScreenBuilt</c> per contributor and menu, which undoes the previous edits, applies the current ones (<see cref="DecorationApplier"/>) and (re)subscribes the <c>On</c> handlers: exactly one subscription per contributor, menu and event, however often the menu opens.</item>
    /// </list>
    /// Expressions see the owner's exposed values as <c>ctx.*</c> (the scope's menu is the target menu); <c>menu.*</c> is
    /// the contribution's own state. After a reload that changed anything, open target menus rebuild their slots and
    /// decorations at once (<see cref="MenuRegistry.NotifyOpening"/>).
    /// </summary>
    internal sealed class ContributionBuilder
    {
        private readonly DataBuilder builder;
        private readonly IValueResolver resolver;
        private readonly ConsumerContexts contexts;
        private readonly Func<string, StardewUIApi> facades;
        private readonly ExtensionRegistry extensions;
        private readonly MenuRegistry menus;
        private readonly DecorationApplier decorations;
        private readonly Dictionary<string, DataContributionRuntime> runtimes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SlotRegistration> slots = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MenuRegistration> decorators = new(StringComparer.Ordinal);
        private string combinedHash = string.Empty;

        internal ContributionBuilder(DataBuilder builder, IValueResolver resolver, ConsumerContexts contexts, Func<string, StardewUIApi> facades, ExtensionRegistry extensions, MenuRegistry menus)
        {
            this.builder = builder;
            this.resolver = resolver;
            this.contexts = contexts;
            this.facades = facades;
            this.extensions = extensions;
            this.menus = menus;
            decorations = new DecorationApplier(builder, resolver);
        }

        /// <summary>The loaded contributions by key.</summary>
        internal IReadOnlyDictionary<string, DataContributionRuntime> Runtimes => runtimes;

        /// <summary>A validated entry of the asset.</summary>
        internal sealed record Entry(string Key, string Contributor, string Name, string TargetOwner, string TargetMenu, int Priority, ContributionDefinition Definition, string Hash, DataMessageLog Log);

        /// <summary>Register the current entries (replacing the previous set); returns the number of open menus refreshed.</summary>
        internal int Apply(IReadOnlyList<Entry> entries)
        {
            string hash = DefinitionHash.Of(entries.Select(e => e.Key + "=" + e.Hash).OrderBy(k => k, StringComparer.Ordinal).ToArray());
            foreach (Entry entry in entries)
            {
                if (runtimes.TryGetValue(entry.Key, out DataContributionRuntime? existing) && existing.Hash == entry.Hash)
                {
                    existing.Messages = entry.Log;
                }
            }

            if (hash == combinedHash)
            {
                return 0;
            }

            combinedHash = hash;
            var affected = new HashSet<(string Owner, string Menu)>(runtimes.Values.Select(r => (r.TargetOwner, r.TargetMenu)));

            // runtimes (kept when unchanged, so their state survives)
            var next = new Dictionary<string, DataContributionRuntime>(StringComparer.Ordinal);
            foreach (Entry entry in entries)
            {
                if (!runtimes.TryGetValue(entry.Key, out DataContributionRuntime? runtime) || runtime.Hash != entry.Hash)
                {
                    runtime = new DataContributionRuntime(entry.Contributor, entry.Name);
                }

                runtime.Definition = entry.Definition;
                runtime.Hash = entry.Hash;
                runtime.Messages = entry.Log;
                runtime.TargetOwner = entry.TargetOwner;
                runtime.TargetMenu = entry.TargetMenu;
                runtime.Priority = entry.Priority;
                runtime.Reported.Clear();
                next[entry.Key] = runtime;
                affected.Add((entry.TargetOwner, entry.TargetMenu));
            }

            runtimes.Clear();
            foreach ((string key, DataContributionRuntime runtime) in next)
            {
                runtimes[key] = runtime;
            }

            RegisterSlots();
            RegisterDecorators();

            // open target menus show the new contributions right away
            int refreshed = 0;
            foreach ((string owner, string menuId) in affected)
            {
                UIMenu? menu = menus.Get(owner, menuId);
                if (menu != null && menu.IsOpen)
                {
                    menus.NotifyOpening(menu);
                    menu.InvalidateLayout();
                    refreshed++;
                }
            }

            return refreshed;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Slots
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A contributor's registration for one slot (the entries it builds, in priority order).</summary>
        private sealed class SlotRegistration
        {
            internal SlotRegistration(string contributor, string owner, string menuId, string slotId)
            {
                Contributor = contributor;
                Owner = owner;
                MenuId = menuId;
                SlotId = slotId;
            }

            internal string Contributor { get; }
            internal string Owner { get; }
            internal string MenuId { get; }
            internal string SlotId { get; }
            internal List<DataContributionRuntime> Items { get; } = new();
        }

        private void RegisterSlots()
        {
            var current = new Dictionary<string, SlotRegistration>(StringComparer.Ordinal);
            foreach (DataContributionRuntime runtime in runtimes.Values)
            {
                string? slot = runtime.Definition.Slot?.Trim();
                if (string.IsNullOrEmpty(slot))
                {
                    continue;
                }

                string key = string.Join("|", runtime.Owner, runtime.TargetOwner, runtime.TargetMenu, slot);
                if (!current.TryGetValue(key, out SlotRegistration? registration))
                {
                    current[key] = registration = new SlotRegistration(runtime.Owner, runtime.TargetOwner, runtime.TargetMenu, slot);
                }

                registration.Items.Add(runtime);
            }

            foreach ((string key, SlotRegistration old) in slots)
            {
                if (!current.ContainsKey(key))
                {
                    facades(old.Contributor).RemoveContribution(old.Owner, old.MenuId, old.SlotId);
                }
            }

            slots.Clear();
            foreach ((string key, SlotRegistration registration) in current)
            {
                registration.Items.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : string.CompareOrdinal(a.Id, b.Id));
                slots[key] = registration;
                StardewUIApi api = facades(registration.Contributor);
                SlotRegistration captured = registration;
                api.ContributeTo(registration.Owner, registration.MenuId, registration.SlotId, registration.Items.Min(i => i.Priority), (host, _) => BuildSlot(api, captured, host));
            }
        }

        /// <summary>Build a contributor's entries into the container the slot handed it (every time the menu opens).</summary>
        private void BuildSlot(StardewUIApi api, SlotRegistration registration, IUIContainer host)
        {
            UIMenu? menu = (host as UIElement)?.OwnerMenu;
            foreach (DataContributionRuntime runtime in registration.Items)
            {
                runtime.SlotGroup.Clear();
                var log = new DataMessageLog();
                var applier = new PropertyApplier(resolver, runtime.SlotGroup, log);
                DataScope scope = DataScope.ForRuntime(runtime, menu);
                builder.BuildTree(api, runtime, applier, host, runtime.Definition.Children, scope, runtime.Path.Field("Children"));
                DecorationApplier.ReportBuild(runtime, log);
                if (menu != null)
                {
                    RefresherGroup group = runtime.SlotGroup;
                    menu.ExtensionRefresh[runtime] = group.Refresh;
                }

                runtime.SlotGroup.Refresh(opening: true);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Decorations and subscriptions
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A contributor's decorator for one menu: its entries' <c>Decorate</c> and <c>On</c>, the undo steps of the last application and the live subscriptions.</summary>
        private sealed class MenuRegistration
        {
            internal MenuRegistration(string contributor, string owner, string menuId)
            {
                Contributor = contributor;
                Owner = owner;
                MenuId = menuId;
                Group = new RefresherGroup(contributor, owner + "/" + menuId + " decorations");
            }

            internal string Contributor { get; }
            internal string Owner { get; }
            internal string MenuId { get; }
            internal List<DataContributionRuntime> Items { get; } = new();
            internal List<Action> Undo { get; } = new();
            internal RefresherGroup Group { get; }
            internal UIMenu? AppliedTo { get; set; }

            /// <summary>The handler subscribed per event, with the screen context it was subscribed through.</summary>
            internal Dictionary<string, (ScreenContext Screen, Action Handler)> Subscriptions { get; } = new(StringComparer.Ordinal);

            /// <summary>Restore the tree (undo steps in reverse order) and stop refreshing live decoration values.</summary>
            internal void Restore()
            {
                for (int i = Undo.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Undo[i]();
                    }
                    catch (Exception ex)
                    {
                        UIServices.Log($"[{Contributor}] could not undo a decoration of {Owner}/{MenuId}: {ex.Message}", LogLevel.Trace);
                    }
                }

                Undo.Clear();
                Group.Clear();
                AppliedTo?.ExtensionRefresh.Remove(this);
                AppliedTo?.InvalidateLayout();
                AppliedTo = null;
            }

            internal void UnsubscribeAll()
            {
                foreach ((string eventName, (ScreenContext screen, Action handler)) in Subscriptions)
                {
                    screen.Unsubscribe(eventName, handler);
                }

                Subscriptions.Clear();
            }
        }

        private void RegisterDecorators()
        {
            var current = new Dictionary<string, MenuRegistration>(StringComparer.Ordinal);
            foreach (DataContributionRuntime runtime in runtimes.Values.OrderBy(r => r.Priority).ThenBy(r => r.Id, StringComparer.Ordinal))
            {
                ContributionDefinition def = runtime.Definition;
                if ((def.Decorate == null || def.Decorate.Count == 0) && (def.On == null || def.On.Count == 0))
                {
                    continue;
                }

                string key = string.Join("|", runtime.Owner, runtime.TargetOwner, runtime.TargetMenu);
                if (!current.TryGetValue(key, out MenuRegistration? registration))
                {
                    if (!decorators.TryGetValue(key, out registration))
                    {
                        registration = new MenuRegistration(runtime.Owner, runtime.TargetOwner, runtime.TargetMenu);
                    }

                    registration.Items.Clear();
                    current[key] = registration;
                }

                registration.Items.Add(runtime);
            }

            // registrations that disappeared: restore the tree, drop the subscriptions and the decorator
            foreach ((string key, MenuRegistration old) in decorators)
            {
                if (!current.ContainsKey(key))
                {
                    old.Restore();
                    old.UnsubscribeAll();
                    facades(old.Contributor).OnScreenBuilt(old.Owner, old.MenuId, null!);
                }
            }

            decorators.Clear();
            foreach ((string key, MenuRegistration registration) in current)
            {
                decorators[key] = registration;
                StardewUIApi api = facades(registration.Contributor);
                MenuRegistration captured = registration;
                api.OnScreenBuilt(registration.Owner, registration.MenuId, menu => Decorate(api, captured, menu));
            }
        }

        /// <summary>The decorator: undo the last application, apply every entry's operations, then (re)subscribe the <c>On</c> handlers.</summary>
        private void Decorate(StardewUIApi api, MenuRegistration registration, IUIMenu target)
        {
            if (target is not UIMenu menu)
            {
                return;
            }

            ConsumerContext contributor = contexts.For(registration.Contributor);
            registration.Restore();
            foreach (DataContributionRuntime runtime in registration.Items)
            {
                decorations.Apply(runtime, menu, api, contributor, registration.Undo, registration.Group);
            }

            registration.AppliedTo = menu;
            if (registration.Group.Count > 0)
            {
                RefresherGroup group = registration.Group;
                menu.ExtensionRefresh[registration] = group.Refresh;
            }

            Subscribe(registration, menu, contributor);
        }

        /// <summary>Exactly one handler per contributor, menu and event: the previous one is unsubscribed before the new one is subscribed.</summary>
        private static void Subscribe(MenuRegistration registration, UIMenu menu, ConsumerContext contributor)
        {
            var handlers = new Dictionary<string, List<DataContributionRuntime>>(StringComparer.Ordinal);
            foreach (DataContributionRuntime runtime in registration.Items)
            {
                if (runtime.Definition.On == null)
                {
                    continue;
                }

                foreach ((string rawEvent, List<ActionDefinition>? actions) in runtime.Definition.On)
                {
                    string eventName = rawEvent?.Trim() ?? string.Empty;
                    if (eventName.Length == 0 || actions == null || actions.Count == 0)
                    {
                        continue;
                    }

                    if (!handlers.TryGetValue(eventName, out List<DataContributionRuntime>? list))
                    {
                        handlers[eventName] = list = new List<DataContributionRuntime>();
                    }

                    list.Add(runtime);
                }
            }

            registration.UnsubscribeAll();
            var screen = new ScreenContext(ScopeRootsExposures(menu), contributor);
            foreach ((string eventName, List<DataContributionRuntime> list) in handlers)
            {
                DataContributionRuntime[] subscribers = list.ToArray();
                string name = eventName;
                Action handler = () => RunHandlers(subscribers, menu, name);
                screen.Subscribe(name, handler);
                registration.Subscriptions[name] = (screen, handler);
            }
        }

        private static ScreenExposures ScopeRootsExposures(UIMenu menu)
        {
            return State.ScopeRoots.Exposures?.Invoke(menu) ?? throw new InvalidOperationException("menu exposures are not available.");
        }

        /// <summary>Run each entry's actions for <paramref name="eventName"/> (all of them run; the first failure is rethrown for the subscriber's guard).</summary>
        private static void RunHandlers(DataContributionRuntime[] subscribers, UIMenu menu, string eventName)
        {
            DataActionException? failure = null;
            foreach (DataContributionRuntime runtime in subscribers)
            {
                List<ActionDefinition>? actions = runtime.Definition.On?.FirstOrDefault(p => string.Equals(p.Key?.Trim(), eventName, StringComparison.Ordinal)).Value;
                try
                {
                    DataActionRunner.Run(actions, DataScope.ForRuntime(runtime, menu).WithEvent("On:" + eventName, null));
                }
                catch (DataActionException ex)
                {
                    failure ??= new DataActionException($"{runtime.Path}.On.{eventName}: {ex.Message}");
                }
            }

            if (failure != null)
            {
                throw failure;
            }
        }
    }
}
