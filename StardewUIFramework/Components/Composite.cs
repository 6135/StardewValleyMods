using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Hosting;

namespace UIFramework.Components
{
    /// <summary>
    /// An instance of a composite (architecture.md §16.1): the host container the definition's builder fills, laid
    /// out as a column. The same object is the <see cref="IUICompositeHost"/> the builder sees (where it exposes
    /// values, commands and events) and the <see cref="IUIComposite"/> the user holds (where it reads them). The
    /// builder runs under the defining mod's guard, and the defining mod's API instance may add children here even
    /// though the menu belongs to another mod (see <see cref="ComponentOwner"/>).
    /// </summary>
    internal sealed class Composite : UIContainer, IUIComposite, IUICompositeHost
    {
        private readonly CompositeRegistry registry;
        private readonly CompositeArgs args;
        private readonly Dictionary<string, Func<string>> values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<double>> numbers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<bool>> bools = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action> commands = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Action>> subscribers = new(StringComparer.Ordinal);
        private ConsumerContext? owner;

        internal Composite(string id, string compositeName, CompositeArgs args, CompositeRegistry registry) : base(id)
        {
            CompositeName = compositeName;
            this.args = args;
            this.registry = registry;
        }

        public string CompositeName { get; private set; }

        IUICompositeArgs IUIComposite.Args => args;

        /// <summary>True when the current definition comes from the <c>Composites</c> data asset (v1.7).</summary>
        internal bool IsDataComposite => registry.Get(CompositeName)?.IsData == true;

        /// <summary>The argument bag (data instances carry their builder payload in it).</summary>
        internal CompositeArgs ArgsBag => args;

        /// <summary>
        /// The refreshers of a data composite's body (v1.7): registered with the menu the instance lives in
        /// (<see cref="UIMenu.ExtensionRefresh"/>) so its live values update in C# and data menus alike. Null for C# composites.
        /// </summary>
        internal Data.RefresherGroup? DataGroup { get; set; }

        /// <summary>The mod whose builder last filled this composite (null until built or when the definition vanished).</summary>
        internal override ConsumerContext? ComponentOwner => owner;

        // a composite is layout-only like a stack: clicks on gaps fall through unless it has its own handlers
        protected override bool IsHitTestVisible => HasPointerHandlers;

        // ---------------------------------------------------------------------------------------------------------
        //  Building
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Resolve the definition by name and run its builder (guarded by the defining mod's context).</summary>
        internal void Build()
        {
            CompositeDefinition? definition = registry.Get(CompositeName);
            if (definition == null)
            {
                UIServices.Log($"[{Consumer.ModId}] composite '{CompositeName}' for element '{Id}' is not defined.", LogLevel.Warn);
                owner = null;
                return;
            }

            owner = definition.Owner;
            Action<IUICompositeHost, IUICompositeArgs> build = definition.Build;
            owner.Invoke(Id, "Composite.Build", () => build(this, args));
        }

        /// <summary>Instantiate another composite in place (a data element whose <c>Composite</c> name is an expression that changed).</summary>
        internal void Retarget(string compositeName)
        {
            CompositeName = compositeName;
            Rebuild();
        }

        /// <summary>A data composite's body refreshes with the menu it is attached to.</summary>
        internal override void SetOwnerMenu(UIMenu? menu)
        {
            UIMenu? previous = OwnerMenu;
            base.SetOwnerMenu(menu);
            if (previous != null && previous != menu)
            {
                previous.ExtensionRefresh.Remove(this);
            }

            if (menu != null && DataGroup != null)
            {
                Data.RefresherGroup group = DataGroup;
                menu.ExtensionRefresh[this] = group.Refresh;
            }
        }

        /// <summary>
        /// An exposed value for data (<c>el[id].&lt;key&gt;</c>): text values first (typed like state), then numbers,
        /// then bools; the key is matched exactly, then case-insensitively. False when nothing is exposed under it.
        /// </summary>
        internal bool TryReadExposed(string key, out object? value)
        {
            value = null;
            string? text = Match(values, key);
            if (text != null)
            {
                value = GetValue(text);
                return true;
            }

            string? number = Match(numbers, key);
            if (number != null)
            {
                value = GetNumber(number);
                return true;
            }

            string? flag = Match(bools, key);
            if (flag != null)
            {
                value = GetBool(flag);
                return true;
            }

            return false;
        }

        private static string? Match<T>(Dictionary<string, T> table, string key)
        {
            if (table.ContainsKey(key))
            {
                return key;
            }

            foreach (string candidate in table.Keys)
            {
                if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        public void Rebuild()
        {
            if (DataGroup != null)
            {
                // a data body registers its refreshers again when it rebuilds (a C# body has none)
                DataGroup.Clear();
                DataGroup = null;
                OwnerMenu?.ExtensionRefresh.Remove(this);
            }

            Clear();
            values.Clear();
            numbers.Clear();
            bools.Clear();
            commands.Clear();
            Build();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Host side (the builder exposes)
        // ---------------------------------------------------------------------------------------------------------

        public void Expose(string key, Func<string> value) => Store(values, key, value);
        public void ExposeNumber(string key, Func<double> value) => Store(numbers, key, value);
        public void ExposeBool(string key, Func<bool> value) => Store(bools, key, value);
        public void ExposeCommand(string key, Action command) => Store(commands, key, command);

        private static void Store<T>(Dictionary<string, T> table, string key, T? value) where T : Delegate
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("A key is required.", nameof(key));
            }

            if (value == null)
            {
                table.Remove(key);
            }
            else
            {
                table[key] = value;
            }
        }

        public void Publish(string eventName)
        {
            if (string.IsNullOrEmpty(eventName) || !subscribers.TryGetValue(eventName, out List<Action>? handlers))
            {
                return;
            }

            Action[] snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                Consumer.Invoke(Id, $"Composite.Event:{eventName}#{i}", snapshot[i]);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  User side (the holder reads)
        // ---------------------------------------------------------------------------------------------------------

        public string GetValue(string key) => Read(values, key, string.Empty);
        public double GetNumber(string key) => Read(numbers, key, 0d);
        public bool GetBool(string key) => Read(bools, key, false);

        private T Read<T>(Dictionary<string, Func<T>> table, string key, T fallback)
        {
            if (key == null || !table.TryGetValue(key, out Func<T>? getter))
            {
                return fallback;
            }

            return Guard.Invoke(Id, "Composite.Value:" + key, getter, fallback);
        }

        public bool HasCommand(string command) => command != null && commands.ContainsKey(command);

        public void Invoke(string command)
        {
            if (command != null && commands.TryGetValue(command, out Action? action))
            {
                Guard.Invoke(Id, "Composite.Command:" + command, action);
            }
        }

        public void Subscribe(string eventName, Action handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
            {
                return;
            }

            if (!subscribers.TryGetValue(eventName, out List<Action>? handlers))
            {
                subscribers[eventName] = handlers = new List<Action>();
            }

            handlers.Add(handler);
        }

        /// <summary>Exposed delegates belong to the defining mod, so its guard runs them (falling back to the menu's consumer).</summary>
        private ConsumerContext Guard => owner ?? Consumer;

        // ---------------------------------------------------------------------------------------------------------
        //  Layout (a column, no spacing)
        // ---------------------------------------------------------------------------------------------------------

        protected override Vector2 MeasureCore(Vector2 available)
        {
            float width = 0, height = 0;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    continue;
                }

                Vector2 size = child.Measure(available);
                width = Math.Max(width, size.X);
                height += size.Y;
            }

            return new Vector2(width, height);
        }

        protected override void ArrangeCore()
        {
            int cursor = Bounds.Y;
            foreach (UIElement child in Children)
            {
                if (!child.Visible)
                {
                    child.Arrange(new Rectangle(Bounds.X, Bounds.Y, 0, 0));
                    continue;
                }

                int extent = (int)Math.Ceiling(child.DesiredSize.Y);
                child.Arrange(new Rectangle(Bounds.X, cursor, Bounds.Width, extent));
                cursor += extent;
            }
        }
    }
}
