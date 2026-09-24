using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Hosting;

namespace UIFramework.Data
{
    /// <summary>
    /// Keeps the data composites of the <c>Composites</c> asset (v1.7) registered in the <see cref="CompositeRegistry"/>
    /// next to the C# ones, so <c>AddComposite</c> (C#) and <c>"Type": "&lt;name&gt;"</c> (data) instantiate them alike.
    /// Each registration's builder looks the current definition up when it runs, so a changed definition only has to
    /// rebuild the live instances (<see cref="Composite.Rebuild"/>). A composite a C# mod defined under the same name
    /// wins; the data entry is skipped.
    /// </summary>
    internal sealed class DataComposites
    {
        private readonly CompositeRegistry registry;
        private readonly ConsumerContexts contexts;
        private readonly Func<string, StardewUIApi> facades;
        private readonly DataBuilder builder;
        private readonly Dictionary<string, DataCompositeRuntime> runtimes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<IUICompositeHost, IUICompositeArgs>> registered = new(StringComparer.Ordinal);

        internal DataComposites(CompositeRegistry registry, ConsumerContexts contexts, Func<string, StardewUIApi> facades, DataBuilder builder)
        {
            this.registry = registry;
            this.contexts = contexts;
            this.facades = facades;
            this.builder = builder;
        }

        /// <summary>The loaded data composites by name.</summary>
        internal IReadOnlyDictionary<string, DataCompositeRuntime> Runtimes => runtimes;

        /// <summary>The definition of data composite <paramref name="name"/>, if one is loaded.</summary>
        internal DataCompositeDefinition? Definition(string name) => runtimes.TryGetValue(name, out DataCompositeRuntime? runtime) ? runtime.Definition : null;

        /// <summary>
        /// Register or update one entry; returns its state for <c>ui_data</c> (<c>built</c>, <c>rebuilt (n instances)</c>,
        /// <c>unchanged</c> or <c>skipped (C# composite)</c>).
        /// </summary>
        internal string Apply(string name, string owner, DataCompositeDefinition def, string hash, DataMessageLog log)
        {
            CompositeDefinition? existing = registry.Get(name);
            registered.TryGetValue(name, out Action<IUICompositeHost, IUICompositeArgs>? mine);
            if (existing != null && existing.Build != mine)
            {
                runtimes.Remove(name);
                registered.Remove(name);
                log.Warn(DataPath.Entry(DataAssets.ShortName(DataAssets.Composites), name), $"{existing.Owner.ModId} defined composite '{name}' in C#; the C# composite wins and this entry is skipped.");
                return "skipped (C# composite)";
            }

            if (runtimes.TryGetValue(name, out DataCompositeRuntime? runtime) && runtime.Hash == hash && runtime.Owner == owner)
            {
                runtime.Messages = log;
                return "built";
            }

            bool rebuild = runtime != null;
            if (runtime == null || runtime.Owner != owner)
            {
                runtime = new DataCompositeRuntime(owner, name);
                runtimes[name] = runtime;
            }

            runtime.Definition = def;
            runtime.Hash = hash;
            runtime.Messages = log;
            runtime.Reported.Clear();

            if (mine == null || existing?.Owner.ModId != owner)
            {
                mine = (host, args) => Build(name, host, args);
                registered[name] = mine;
                registry.Define(contexts.For(owner), name, mine, isData: true);
            }

            // instances created before the definition existed (or changed) build the new body now
            List<Composite> instances = registry.LiveInstances(name);
            foreach (Composite instance in instances)
            {
                instance.Rebuild();
            }

            return rebuild ? $"rebuilt ({instances.Count} instance(s))" : instances.Count > 0 ? $"built ({instances.Count} instance(s))" : "built";
        }

        /// <summary>Unregister the entries that disappeared from the asset (their instances become empty).</summary>
        internal void RemoveMissing(ICollection<string> seen, IMonitor monitor)
        {
            foreach (string name in new List<string>(registered.Keys))
            {
                if (seen.Contains(name))
                {
                    continue;
                }

                Action<IUICompositeHost, IUICompositeArgs> mine = registered[name];
                registered.Remove(name);
                runtimes.Remove(name);
                CompositeDefinition? current = registry.Get(name);
                if (current != null && current.Build == mine && registry.Undefine(current.Owner, name))
                {
                    foreach (Composite instance in registry.LiveInstances(name))
                    {
                        instance.Rebuild();
                    }

                    monitor.Log($"Data composite '{name}' was removed from {DataAssets.Composites}.", LogLevel.Trace);
                }
            }
        }

        /// <summary>The registered builder: the current definition's body, built through the composite owner's facade.</summary>
        private void Build(string name, IUICompositeHost host, IUICompositeArgs args)
        {
            if (!runtimes.TryGetValue(name, out DataCompositeRuntime? runtime) || host is not Composite composite || args is not CompositeArgs bag)
            {
                return;
            }

            builder.BuildCompositeBody(composite, bag, runtime, facades(runtime.Owner));
        }
    }
}
