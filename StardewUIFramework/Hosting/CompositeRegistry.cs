using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>A composite definition: who registered it and the builder that fills a host.</summary>
    internal sealed class CompositeDefinition
    {
        internal CompositeDefinition(string name, ConsumerContext owner, Action<IUICompositeHost, IUICompositeArgs> build)
        {
            Name = name;
            Owner = owner;
            Build = build;
        }

        /// <summary>Global name (convention <c>"&lt;ModId&gt;.&lt;Name&gt;"</c>).</summary>
        internal string Name { get; }

        /// <summary>The mod that defined it; its guard runs the builder and only it may undefine the composite.</summary>
        internal ConsumerContext Owner { get; }

        /// <summary>Fills a host with elements (architecture.md §16.1, "Reusable composite components").</summary>
        internal Action<IUICompositeHost, IUICompositeArgs> Build { get; }
    }

    /// <summary>
    /// Process-wide table of composite definitions. Names are global strings so a composite registered by one mod
    /// can be instantiated by any other; the defining mod is remembered so only it can remove the definition.
    /// </summary>
    internal sealed class CompositeRegistry
    {
        private readonly Dictionary<string, CompositeDefinition> definitions = new(StringComparer.Ordinal);

        /// <summary>Register (or replace) a definition.</summary>
        internal void Define(ConsumerContext owner, string name, Action<IUICompositeHost, IUICompositeArgs> build)
        {
            if (definitions.TryGetValue(name, out CompositeDefinition? existing))
            {
                UIServices.Log($"[{owner.ModId}] composite '{name}' (defined by {existing.Owner.ModId}) is replaced.", LogLevel.Debug);
            }

            definitions[name] = new CompositeDefinition(name, owner, build);
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
