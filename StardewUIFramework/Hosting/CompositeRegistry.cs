using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>A composite definition: who registered it and the builder that fills a host.</summary>
    internal sealed class CompositeDefinition
    {
        internal CompositeDefinition(string name, ConsumerContext owner, Action<IUICompositeHost, IUICompositeArgs> build, bool isData = false)
        {
            Name = name;
            Owner = owner;
            Build = build;
            IsData = isData;
        }

        /// <summary>True for a composite defined in the <c>Composites</c> data asset (v1.7).</summary>
        internal bool IsData { get; }

        /// <summary>Global name (convention <c>"&lt;ModId&gt;.&lt;Name&gt;"</c>).</summary>
        internal string Name { get; }

        /// <summary>The mod that defined it; its guard runs the builder and only it may undefine the composite.</summary>
        internal ConsumerContext Owner { get; }

        /// <summary>Fills a host with elements (architecture.md §16.1, "Reusable composite components").</summary>
        internal Action<IUICompositeHost, IUICompositeArgs> Build { get; }
    }

    /// <summary>
    /// Process-wide table of composite definitions. Names are global strings so a composite registered by one mod
    /// can be instantiated by any other; the defining mod is remembered so only it can replace or remove the definition.
    /// </summary>
    internal sealed class CompositeRegistry
    {
        private readonly Dictionary<string, CompositeDefinition> definitions = new(StringComparer.Ordinal);
        private readonly List<WeakReference<Composite>> instances = new();

        /// <summary>Register a definition, or replace one <paramref name="owner"/> defined; false (logged) when another mod owns the name.</summary>
        internal bool Define(ConsumerContext owner, string name, Action<IUICompositeHost, IUICompositeArgs> build, bool isData = false)
        {
            if (definitions.TryGetValue(name, out CompositeDefinition? existing))
            {
                if (existing.Owner.ModId != owner.ModId)
                {
                    UIServices.Log($"[{owner.ModId}] cannot define composite '{name}': it belongs to {existing.Owner.ModId} (prefix your composite names with your mod id).", LogLevel.Warn);
                    return false;
                }

                UIServices.Log($"[{owner.ModId}] composite '{name}' is replaced.", LogLevel.Debug);
            }

            definitions[name] = new CompositeDefinition(name, owner, build, isData);
            return true;
        }

        /// <summary>Remember an instance (weakly) so a changed data definition can rebuild it (v1.7).</summary>
        internal void Track(Composite instance)
        {
            if (instances.Count >= 64 && instances.Count % 64 == 0)
            {
                instances.RemoveAll(w => !w.TryGetTarget(out _));
            }

            instances.Add(new WeakReference<Composite>(instance));
        }

        /// <summary>The live instances of <paramref name="name"/> that are attached to a menu.</summary>
        internal List<Composite> LiveInstances(string name)
        {
            var result = new List<Composite>();
            instances.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (WeakReference<Composite> reference in instances)
            {
                if (reference.TryGetTarget(out Composite? instance) && instance.OwnerMenu != null && instance.CompositeName == name)
                {
                    result.Add(instance);
                }
            }

            return result;
        }

        /// <summary>Remove <paramref name="name"/> if <paramref name="owner"/> defined it; returns false otherwise.</summary>
        internal bool Undefine(ConsumerContext owner, string name)
        {
            if (!definitions.TryGetValue(name, out CompositeDefinition? existing))
            {
                return false;
            }

            if (existing.Owner.ModId != owner.ModId)
            {
                UIServices.Log($"[{owner.ModId}] cannot undefine composite '{name}': it belongs to {existing.Owner.ModId}.", LogLevel.Warn);
                return false;
            }

            definitions.Remove(name);
            return true;
        }

        internal CompositeDefinition? Get(string name)
        {
            return definitions.TryGetValue(name, out CompositeDefinition? definition) ? definition : null;
        }

        internal bool Has(string name) => definitions.ContainsKey(name);

        /// <summary>Every defined name, sorted.</summary>
        internal string[] List()
        {
            var names = new string[definitions.Count];
            definitions.Keys.CopyTo(names, 0);
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }
    }
}
