using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data
{
    /// <summary>Something re-applied by a data menu's refresh hook (<see cref="UIMenu.DataRefresh"/>).</summary>
    internal interface IDataRefresher
    {
        /// <summary>Re-apply; <paramref name="opening"/> is true once per open (and after an in-place rebuild of an open menu).</summary>
        void Refresh(bool opening);
    }

    /// <summary>
    /// An ordered list of refreshers that is itself a refresher. Structural elements (<c>If</c>, <c>Switch</c> pages)
    /// give their subtree its own group, refreshed only while the subtree is built and dropped with it. A faulting
    /// refresher is logged once and skipped afterwards.
    /// </summary>
    internal sealed class RefresherGroup : IDataRefresher
    {
        private readonly List<IDataRefresher> items = new();
        private readonly HashSet<IDataRefresher> faulted = new();
        private readonly string owner;
        private readonly string id;

        internal RefresherGroup(string owner, string id)
        {
            this.owner = owner;
            this.id = id;
        }

        /// <summary>Number of refreshers (diagnostics).</summary>
        internal int Count => items.Count;

        internal void Add(IDataRefresher refresher) => items.Add(refresher);

        internal void Clear()
        {
            items.Clear();
            faulted.Clear();
        }

        public void Refresh(bool opening)
        {
            // index loop: refreshing an If can add refreshers to a child group, never to this one
            for (int i = 0; i < items.Count; i++)
            {
                IDataRefresher refresher = items[i];
                if (faulted.Contains(refresher))
                {
                    continue;
                }

                try
                {
                    refresher.Refresh(opening);
                }
                catch (Exception ex)
                {
                    faulted.Add(refresher);
                    UIServices.Log($"[{owner}] a data value of '{id}' failed to refresh and is no longer updated: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }

    /// <summary>How long a data menu's <c>menu.*</c> state lives.</summary>
    internal enum StateLifetime
    {
        /// <summary>Until the return to title (default).</summary>
        Session,

        /// <summary>Reset to the defaults every time the menu opens.</summary>
        Open
    }

    /// <summary>
    /// What the data layer keeps for one built data UI (a menu or a HUD): identity, messages, the refresh list the
    /// builder registers dynamic values in, the <c>Computed</c> values and <c>Watch</c>es, the per-element validation
    /// errors, and the open generation that one-time <c>$:{...}</c> values follow.
    /// </summary>
    internal abstract class DataRuntime
    {
        private readonly Dictionary<string, Computed> computed = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(StateAddress Address, List<ActionDefinition> Actions)> watches = new();
        private readonly PerScreen<Dictionary<string, string>> errors = new(() => new Dictionary<string, string>(StringComparer.Ordinal));
        private readonly HashSet<string> computing = new(StringComparer.OrdinalIgnoreCase);

        protected DataRuntime(string owner, string id, DataPath path)
        {
            Owner = owner;
            Id = id;
            Path = path;
            Refreshers = new RefresherGroup(owner, id);
        }

        internal string Owner { get; }

        /// <summary>The menu or HUD id (without the owner).</summary>
        internal string Id { get; }

        /// <summary>The <c>owner/id</c> key of the entry.</summary>
        internal string Key => Owner + "/" + Id;

        /// <summary>The container of this UI's <c>menu.*</c> state (<c>owner/menu</c>, or <c>owner/hud:id</c> for HUDs).</summary>
        internal abstract string StateKey { get; }

        /// <summary>Path of the entry, for messages.</summary>
        internal DataPath Path { get; }

        /// <summary>Content hash of the definition of the last build.</summary>
        internal string Hash { get; set; } = string.Empty;

        /// <summary>Validation and build messages of the last build.</summary>
        internal DataMessageLog Messages { get; set; } = new();

        /// <summary>The menu model the tree lives in (the menu, or the HUD's hidden inner menu).</summary>
        internal abstract UIMenu? Model { get; }

        /// <summary>The UI-level scope of the last build (Computed values and Watches run in it).</summary>
        internal DataScope? Scope { get; set; }

        /// <summary>How long <c>menu.*</c> state lives.</summary>
        internal StateLifetime Lifetime { get; set; }

        /// <summary>Values re-applied by <see cref="Refresh"/>: dynamic properties, conditions, structural elements, output bindings.</summary>
        internal RefresherGroup Refreshers { get; }

        /// <summary>Incremented every time the UI opens (or a HUD is shown); one-time <c>$:{...}</c> values re-evaluate when it changes.</summary>
        internal int OpenGeneration { get; private set; }

        /// <summary>The UI's named row sources (<c>Sources</c>), by name.</summary>
        internal Dictionary<string, SourceDefinition> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Collections built by the last build, by element id (<c>_Sort</c>, <c>_Rebuild</c>, forms...).</summary>
        internal Dictionary<string, Building.IDataCollection> Collections { get; } = new(StringComparer.Ordinal);

        /// <summary>Messages already logged at run time (reported once per build).</summary>
        internal HashSet<string> Reported { get; } = new(StringComparer.Ordinal);

        /// <summary>The UI's own templates (a menu's <c>Templates</c>, v1.7), by name; owner-wide ones come from <c>Owners</c>.</summary>
        internal Dictionary<string, TemplateDefinition> Templates { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Forget the refreshers, computed values, watches, sources and collections of the previous build.</summary>
        internal void ResetBuild()
        {
            Refreshers.Clear();
            computed.Clear();
            watches.Clear();
            Sources.Clear();
            Collections.Clear();
            Templates.Clear();
            Reported.Clear();
        }

        /// <summary>The refresh hook: bump the open generation (and reset <c>StateLifetime: Open</c> state) when opening, then run every refresher.</summary>
        internal void Refresh(UIMenu menu, bool opening)
        {
            if (opening)
            {
                OpenGeneration++;
                if (Lifetime == StateLifetime.Open)
                {
                    DataStateStore.Active?.ResetContainer(StateScope.Menu, StateKey);
                }
            }

            Refreshers.Refresh(opening);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Computed values
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Define <c>menu.&lt;name&gt;</c> as an expression evaluated in the UI scope.</summary>
        internal void AddComputed(string name, CompiledExpression expression) => computed[name] = new Computed(expression);

        /// <summary>True when <paramref name="name"/> is a computed value.</summary>
        internal bool HasComputed(string name) => computed.ContainsKey(name);

        /// <summary>Evaluate computed value <paramref name="name"/> (cached per screen and state epoch); null on a cycle.</summary>
        internal bool TryComputed(string name, out DataValue value, out bool isVolatile)
        {
            value = DataValue.Null;
            isVolatile = false;
            if (!computed.TryGetValue(name, out Computed? entry) || Scope == null)
            {
                return false;
            }

            if (!computing.Add(name))
            {
                UIServices.Log($"[{Owner}] computed value '{name}' of '{Id}' depends on itself.", LogLevel.Warn);
                return true;
            }

            try
            {
                int screen = DataStateStore.Screen;
                long epoch = DataStateStore.Active?.Epoch(screen) ?? 0;
                ExpressionResult result = entry.Expression.Evaluate(Scope, entry.Cache, screen, epoch, DataEnvironment.Tick);
                if (!result.Succeeded && !entry.Reported)
                {
                    entry.Reported = true;
                    UIServices.Log($"[{Owner}] computed value '{name}' of '{Id}': {result.Error}", LogLevel.Warn);
                }

                value = result.Value;
                isVolatile = result.IsVolatile;
                return true;
            }
            finally
            {
                computing.Remove(name);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Watches
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Run <paramref name="actions"/> whenever <paramref name="address"/> changes.</summary>
        internal void AddWatch(StateAddress address, List<ActionDefinition> actions) => watches.Add((address, actions));

        /// <summary>Run the watches of a changed value (called by <see cref="DataService"/>).</summary>
        internal void OnStateChanged(StateAddress address, DataValue oldValue, DataValue newValue)
        {
            if (watches.Count == 0 || Scope == null)
            {
                return;
            }

            foreach ((StateAddress watched, List<ActionDefinition> actions) in watches.ToArray())
            {
                if (watched.Scope != address.Scope || !string.Equals(watched.Container, address.Container, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(watched.Name, address.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] = DataValue.FromString(address.ToString()),
                    ["old"] = oldValue,
                    ["new"] = newValue,
                    ["value"] = newValue
                };
                DataScope scope = Scope.WithEvent("Watch", null, fields);
                ConsumerContext context = Scope.Consumer;
                context.Invoke(Id, "Watch " + watched, () => Actions.DataActionRunner.Run(actions, scope));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Validation errors (el[id].error, self.error)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Remember (or with null, clear) the validation error of an element on the current screen.</summary>
        internal void SetError(string elementId, string? error)
        {
            if (error == null)
            {
                errors.Value.Remove(elementId);
            }
            else
            {
                errors.Value[elementId] = error;
            }
        }

        /// <summary>The validation error of an element on the current screen, or null.</summary>
        internal string? ErrorOf(string elementId) => errors.Value.TryGetValue(elementId, out string? error) ? error : null;

        private sealed class Computed
        {
            internal Computed(CompiledExpression expression)
            {
                Expression = expression;
            }

            internal CompiledExpression Expression { get; }
            internal EvaluationCache Cache { get; } = new();
            internal bool Reported { get; set; }
        }
    }

    /// <summary>
    /// A built data menu: its definition and hash (for the reload diff), the menu model, and everything of
    /// <see cref="DataRuntime"/>. Input values live in the state store (<c>menu.*</c>), so they survive rebuilds.
    /// </summary>
    internal sealed class DataMenuRuntime : DataRuntime
    {
        internal DataMenuRuntime(string owner, string menuId) : base(owner, menuId, DataPath.Entry(DataAssets.ShortName(DataAssets.Menus), owner + "/" + menuId))
        {
        }

        /// <summary>The menu id (without the owner).</summary>
        internal string MenuId => Id;

        internal override string StateKey => Key;

        /// <summary>The (normalized) definition of the last build.</summary>
        internal MenuDefinition Definition { get; set; } = new();

        /// <summary>The menu model built from the definition.</summary>
        internal UIMenu? Menu { get; set; }

        internal override UIMenu? Model => Menu;

        /// <summary>The keys the last build exposed (<c>Expose</c>), removed again before the next build applies its own.</summary>
        internal List<string> ExposedKeys { get; } = new();

        /// <summary>The commands the last build exposed (<c>Commands</c>).</summary>
        internal List<string> CommandKeys { get; } = new();

        /// <summary>True when the last build bound the menu's toggle hotkey (<c>Hotkey</c>); only then does a build without one unbind it.</summary>
        internal bool HotkeyBound { get; set; }
    }

    /// <summary>A built data HUD: its definition, the widget and its per-screen data visibility (<c>ShowHud</c> / <c>HideHud</c>).</summary>
    internal sealed class DataHudRuntime : DataRuntime
    {
        private readonly PerScreen<bool?> visible = new();
        private readonly PerScreen<bool> wasShown = new();

        internal DataHudRuntime(string owner, string hudId) : base(owner, hudId, DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), owner + "/" + hudId))
        {
        }

        internal override string StateKey => Owner + "/hud:" + Id;

        /// <summary>The (normalized) definition of the last build.</summary>
        internal HudDefinition Definition { get; set; } = new();

        /// <summary>The HUD widget.</summary>
        internal UIHud? Hud { get; set; }

        internal override UIMenu? Model => Hud?.Inner;

        /// <summary>The initial data visibility (the definition's <c>Visible</c>).</summary>
        internal bool DefaultVisible { get; set; } = true;

        /// <summary>The data visibility on the current screen (<c>ShowHud</c> / <c>HideHud</c> / <c>ToggleHud</c> / hotkey).</summary>
        internal bool Visible
        {
            get => visible.Value ?? DefaultVisible;
            set => visible.Value = value;
        }

        /// <summary>Track shown transitions per screen; true when the HUD just became shown on this screen (a one-time "open").</summary>
        internal bool MarkShown(bool shown)
        {
            bool opened = shown && !wasShown.Value;
            wasShown.Value = shown;
            return opened;
        }
    }

    /// <summary>
    /// A data composite (v1.7, the <c>Composites</c> asset): its definition, owner and messages. Every instance builds its
    /// body in this runtime (its <c>menu.*</c> is the composite's own container, shared by its instances; bodies should
    /// use <c>args.*</c>); each instance keeps its own refreshers (<see cref="Components.Composite.DataGroup"/>).
    /// </summary>
    internal sealed class DataCompositeRuntime : DataRuntime
    {
        internal DataCompositeRuntime(string owner, string name) : base(owner, name, DataPath.Entry(DataAssets.ShortName(DataAssets.Composites), name))
        {
        }

        /// <summary>The composite's global name.</summary>
        internal string Name => Id;

        internal override string StateKey => Owner + "/composite:" + Id;

        internal override UIMenu? Model => null;

        /// <summary>The (normalized) definition of the last load.</summary>
        internal DataCompositeDefinition Definition { get; set; } = new();
    }

    /// <summary>
    /// A contribution (v1.7, the <c>Contributions</c> asset) of a contributor to another mod's menu: its definition and the
    /// refreshers of what it built the last time the menu opened.
    /// </summary>
    internal sealed class DataContributionRuntime : DataRuntime
    {
        internal DataContributionRuntime(string contributor, string name) : base(contributor, name, DataPath.Entry(DataAssets.ShortName(DataAssets.Contributions), contributor + "/" + name))
        {
            SlotGroup = new RefresherGroup(contributor, name);
        }

        internal override string StateKey => Owner + "/contribution:" + Id;

        internal override UIMenu? Model => null;

        /// <summary>The (normalized) definition of the last load.</summary>
        internal ContributionDefinition Definition { get; set; } = new();

        /// <summary>The target menu's owner.</summary>
        internal string TargetOwner { get; set; } = string.Empty;

        /// <summary>The target menu's id.</summary>
        internal string TargetMenu { get; set; } = string.Empty;

        /// <summary>The priority among the slot's contributions.</summary>
        internal int Priority { get; set; }

        /// <summary>Refreshers of the slot content built the last time the menu opened.</summary>
        internal RefresherGroup SlotGroup { get; }
    }

    /// <summary>Clock and screen values shared by the data layer (the tick keys volatile expression caches).</summary>
    internal static class DataEnvironment
    {
        /// <summary>The game tick (volatile values are cached within one tick).</summary>
        internal static long Tick => StardewValley.Game1.ticks;
    }
}
