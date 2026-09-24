using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;

namespace UIFramework.Data.State
{
    /// <summary>
    /// The <c>config.*</c> scope: one flat string dictionary per owner, global across saves and screens, stored with
    /// <c>helper.Data.WriteJsonFile("data/&lt;owner&gt;.json")</c> in the framework's folder. A file is read the first
    /// time its owner's config is touched and written (debounced) shortly after the last change, so a dragged slider
    /// does not write the file every frame. Gives content packs a real in-game settings screen.
    /// </summary>
    internal sealed class ConfigStore
    {
        /// <summary>Ticks after the last change before the file is written.</summary>
        private const int SaveDelayTicks = 30;

        private readonly IDataHelper? data;
        private readonly IMonitor? monitor;
        private readonly Dictionary<string, Dictionary<string, string>> owners = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> dirty = new(StringComparer.OrdinalIgnoreCase);
        private int ticksSinceChange;

        internal ConfigStore(IDataHelper? data, IMonitor? monitor)
        {
            this.data = data;
            this.monitor = monitor;
        }

        /// <summary>Incremented on every change (part of the state epoch).</summary>
        internal long Version { get; private set; }

        /// <summary>The stored text of <paramref name="name"/>, or null.</summary>
        internal string? Get(string owner, string name)
        {
            return Values(owner).TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>Store (or, with null, remove) a value; returns true when it changed.</summary>
        internal bool Set(string owner, string name, string? value)
        {
            Dictionary<string, string> values = Values(owner);
            if (value == null ? !values.Remove(name) : values.TryGetValue(name, out string? old) && old == value)
            {
                return false;
            }

            if (value != null)
            {
                values[name] = value;
            }

            dirty.Add(owner);
            ticksSinceChange = 0;
            Version++;
            return true;
        }

        /// <summary>Every value of <paramref name="owner"/>.</summary>
        internal IReadOnlyDictionary<string, string> All(string owner) => Values(owner);

        /// <summary>The owners whose config was loaded.</summary>
        internal IEnumerable<string> LoadedOwners => owners.Keys.ToArray();

        /// <summary>Write pending changes once the debounce delay passed (call every tick).</summary>
        internal void OnUpdateTicked()
        {
            if (dirty.Count > 0 && ++ticksSinceChange >= SaveDelayTicks)
            {
                Flush();
            }
        }

        /// <summary>Write every pending change now.</summary>
        internal void Flush()
        {
            foreach (string owner in dirty.ToArray())
            {
                dirty.Remove(owner);
                if (data == null || !IsSafeFileName(owner))
                {
                    continue;
                }

                try
                {
                    data.WriteJsonFile(FileFor(owner), new Dictionary<string, string>(owners[owner], StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    monitor?.Log($"Could not save the data config of '{owner}': {ex.Message}", LogLevel.Warn);
                }
            }
        }

        private Dictionary<string, string> Values(string owner)
        {
            if (owners.TryGetValue(owner, out Dictionary<string, string>? values))
            {
                return values;
            }

            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (data != null && IsSafeFileName(owner))
            {
                try
                {
                    Dictionary<string, string>? stored = data.ReadJsonFile<Dictionary<string, string>>(FileFor(owner));
                    if (stored != null)
                    {
                        foreach ((string key, string value) in stored)
                        {
                            if (key != null && value != null)
                            {
                                values[key] = value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    monitor?.Log($"Could not read the data config of '{owner}': {ex.Message}", LogLevel.Warn);
                }
            }

            owners[owner] = values;
            return values;
        }

        private static string FileFor(string owner) => $"data/{owner}.json";

        private static bool IsSafeFileName(string owner)
        {
            return owner.Length > 0 && owner.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !owner.Contains("..", StringComparison.Ordinal);
        }
    }
}
