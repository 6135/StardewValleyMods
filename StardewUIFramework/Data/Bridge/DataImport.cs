using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using UIFramework.Core;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// Data UIs imported from C# (<c>ImportData</c>, <c>ImportDataFile</c>): each import is a layer of entries for the
    /// <c>Menus</c>, <c>Huds</c>, <c>Sprites</c>, <c>Owners</c>, <c>Composites</c> and <c>Contributions</c> assets. The framework serves the layers as the
    /// assets' base content (<see cref="DataAssets.OnAssetRequested"/>), so Content Patcher packs still patch them, and
    /// invalidates the assets after every import (changed entries then rebuild in place). Imported files can be
    /// watched: a save re-imports the file (debounced) on the next update tick; <c>ui_reload</c> re-reads every file.
    /// <para>
    /// JSON shape: <c>{ "Menus": { "main": {...} }, "Huds": {...}, "Sprites": {...}, "Owner": {...}, "Composites": {...},
    /// "Contributions": {...} }</c> (or <c>"Owners": { "&lt;ModId&gt;": {...} }</c>); keys without an owner get the importing
    /// mod's id (composite names without a dot become <c>&lt;ModId&gt;.&lt;name&gt;</c>), keys of another owner are rejected. A root without any of these members is read as the <c>Menus</c> entries.
    /// </para>
    /// </summary>
    internal static class DataImport
    {
        private const int DebounceMs = 300;

        private static readonly object Sync = new();
        private static readonly List<Layer> Layers = new();
        private static readonly Dictionary<string, WatchedFile> Files = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>One import: the entries of each asset, by full key.</summary>
        private sealed class Layer
        {
            internal Layer(string owner, string source)
            {
                Owner = owner;
                Source = source;
            }

            internal string Owner { get; }
            internal string Source { get; }
            internal Dictionary<string, JToken> Menus { get; } = new(StringComparer.Ordinal);
            internal Dictionary<string, JToken> Huds { get; } = new(StringComparer.Ordinal);
            internal Dictionary<string, JToken> Sprites { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string, JToken> Owners { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal Dictionary<string, JToken> Composites { get; } = new(StringComparer.Ordinal);
            internal Dictionary<string, JToken> Contributions { get; } = new(StringComparer.Ordinal);
        }

        /// <summary>A file imported with <c>ImportDataFile</c>.</summary>
        private sealed class WatchedFile
        {
            internal WatchedFile(string owner, string path)
            {
                Owner = owner;
                Path = path;
            }

            internal string Owner { get; }
            internal string Path { get; }
            internal FileSystemWatcher? Watcher { get; set; }

            /// <summary>When the file last changed on disk (set from the watcher's thread), or null.</summary>
            internal DateTime? ChangedAt { get; set; }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Import
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Import <paramref name="json"/> for <paramref name="owner"/>. <paramref name="file"/> (null for inline JSON) names the layer: an
        /// inline import adds / replaces entries of the owner's inline layer, a file import replaces the file's layer.
        /// </summary>
        internal static bool Import(string owner, string json, string? file, out string error)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json ?? string.Empty, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore });
            }
            catch (Exception ex)
            {
                error = $"the JSON could not be read: {ex.Message}";
                return false;
            }

            var messages = new List<string>();
            lock (Sync)
            {
                string source = file ?? "inline";
                Layer? layer = Layers.FirstOrDefault(l => l.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && l.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
                if (layer == null || file != null)
                {
                    if (layer != null)
                    {
                        Layers.Remove(layer);
                    }

                    layer = new Layer(owner, source);
                    Layers.Add(layer);
                }

                bool any = false;
                foreach (JProperty property in root.Properties())
                {
                    switch (property.Name.ToLowerInvariant())
                    {
                        case "menus":
                            any = true;
                            AddEntries(layer.Menus, property.Value, owner, "Menus", messages);
                            break;
                        case "huds":
                            any = true;
                            AddEntries(layer.Huds, property.Value, owner, "Huds", messages);
                            break;
                        case "sprites":
                            any = true;
                            AddEntries(layer.Sprites, property.Value, owner, "Sprites", messages);
                            break;
                        case "composites":
                            any = true;
                            AddComposites(layer.Composites, property.Value, owner, messages);
                            break;
                        case "contributions":
                            any = true;
                            AddEntries(layer.Contributions, property.Value, owner, "Contributions", messages);
                            break;
                        case "owner":
                            any = true;
                            layer.Owners[owner] = property.Value.DeepClone();
                            break;
                        case "owners":
                            any = true;
                            if (property.Value is JObject owners)
                            {
                                foreach (JProperty entry in owners.Properties())
                                {
                                    if (entry.Name.Trim().Equals(owner, StringComparison.OrdinalIgnoreCase))
                                    {
                                        layer.Owners[owner] = entry.Value.DeepClone();
                                    }
                                    else
                                    {
                                        messages.Add($"Owners: '{entry.Name}' is not {owner}; a mod can only import its own settings.");
                                    }
                                }
                            }

                            break;
                        default:
                            if (property.Name.StartsWith('$'))
                            {
                                any = true; // "$schema"
                            }

                            break;
                    }
                }

                if (!any)
                {
                    AddEntries(layer.Menus, root, owner, "Menus", messages);
                }
            }

            foreach (string message in messages)
            {
                UIServices.Log($"[{owner}] {(file != null ? Path.GetFileName(file) : "ImportData")}: {message}", LogLevel.Warn);
            }

            Invalidate();
            error = string.Empty;
            return true;
        }

        private static void AddEntries(Dictionary<string, JToken> target, JToken token, string owner, string asset, List<string> messages)
        {
            if (token is not JObject entries)
            {
                messages.Add($"{asset} must be an object of entries.");
                return;
            }

            foreach (JProperty entry in entries.Properties())
            {
                if (entry.Name.StartsWith('$'))
                {
                    continue;
                }

                string key = entry.Name.Trim();
                int slash = key.IndexOf('/');
                if (slash < 0)
                {
                    key = owner + "/" + key;
                }
                else if (!key.Substring(0, slash).Equals(owner, StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add($"{asset}: '{entry.Name}' belongs to another owner; a mod can only import its own entries.");
                    continue;
                }

                target[key] = entry.Value.DeepClone();
            }
        }

        /// <summary>Composite entries: global names that must be the owner's (<c>&lt;ModId&gt;.&lt;Name&gt;</c>; a name without a dot is prefixed).</summary>
        private static void AddComposites(Dictionary<string, JToken> target, JToken token, string owner, List<string> messages)
        {
            if (token is not JObject entries)
            {
                messages.Add("Composites must be an object of entries.");
                return;
            }

            foreach (JProperty entry in entries.Properties())
            {
                if (entry.Name.StartsWith('$'))
                {
                    continue;
                }

                string name = entry.Name.Trim();
                if (!name.Contains('.'))
                {
                    name = owner + "." + name;
                }
                else if (!name.StartsWith(owner + ".", StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add($"Composites: '{entry.Name}' does not start with '{owner}.'; a mod can only import its own composites.");
                    continue;
                }

                target[name] = entry.Value.DeepClone();
            }
        }

        /// <summary>Invalidate the data assets so the next read includes the layers.</summary>
        private static void Invalidate()
        {
            IGameContentHelper? content = UIServices.GameContent;
            if (content == null)
            {
                return;
            }

            content.InvalidateCache(DataAssets.Menus);
            content.InvalidateCache(DataAssets.Huds);
            content.InvalidateCache(DataAssets.Sprites);
            content.InvalidateCache(DataAssets.Owners);
            content.InvalidateCache(DataAssets.Composites);
            content.InvalidateCache(DataAssets.Contributions);
            UIServices.Data?.MarkDirty();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Asset base content
        // ---------------------------------------------------------------------------------------------------------

        internal static Dictionary<string, MenuDefinition> Menus() => Build<MenuDefinition>(l => l.Menus, StringComparer.Ordinal);

        internal static Dictionary<string, HudDefinition> Huds() => Build<HudDefinition>(l => l.Huds, StringComparer.Ordinal);

        internal static Dictionary<string, SpriteDefinition> Sprites() => Build<SpriteDefinition>(l => l.Sprites, StringComparer.Ordinal);

        internal static Dictionary<string, OwnerDefinition> Owners() => Build<OwnerDefinition>(l => l.Owners, StringComparer.Ordinal);

        internal static Dictionary<string, DataCompositeDefinition> Composites() => Build<DataCompositeDefinition>(l => l.Composites, StringComparer.Ordinal);

        internal static Dictionary<string, ContributionDefinition> Contributions() => Build<ContributionDefinition>(l => l.Contributions, StringComparer.Ordinal);

        /// <summary>The entries of every layer (later layers win), deserialized; an entry that cannot be read is logged and left out.</summary>
        private static Dictionary<string, T> Build<T>(Func<Layer, Dictionary<string, JToken>> select, StringComparer comparer) where T : class
        {
            var result = new Dictionary<string, T>(comparer);
            lock (Sync)
            {
                foreach (Layer layer in Layers)
                {
                    foreach ((string key, JToken token) in select(layer))
                    {
                        try
                        {
                            T? value = token.ToObject<T>();
                            if (value != null)
                            {
                                result[key] = value;
                            }
                        }
                        catch (Exception ex)
                        {
                            UIServices.Log($"[{layer.Owner}] imported entry '{key}' could not be read: {ex.Message}", LogLevel.Error);
                        }
                    }
                }
            }

            return result;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Files
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Import a file by full path (a C# mod builds it from its own <c>helper.DirectoryPath</c>) and optionally watch it.</summary>
        internal static bool ImportFile(string owner, string path, bool watch, out string error)
        {
            if (!Path.IsPathRooted(path))
            {
                error = $"'{path}' is not a full path; pass Path.Combine(helper.DirectoryPath, \"{path.Replace('\\', '/')}\").";
                return false;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                error = $"'{path}' is not a valid path: {ex.Message}";
                return false;
            }

            if (!ReadFile(owner, full, out error))
            {
                return false;
            }

            lock (Sync)
            {
                if (!Files.TryGetValue(full, out WatchedFile? file))
                {
                    Files[full] = file = new WatchedFile(owner, full);
                }

                if (watch && file.Watcher == null)
                {
                    file.Watcher = CreateWatcher(file);
                }
                else if (!watch && file.Watcher != null)
                {
                    file.Watcher.Dispose();
                    file.Watcher = null;
                }
            }

            return true;
        }

        private static bool ReadFile(string owner, string full, out string error)
        {
            string json;
            try
            {
                json = File.ReadAllText(full);
            }
            catch (Exception ex)
            {
                error = $"'{full}' could not be read: {ex.Message}";
                return false;
            }

            if (!Import(owner, json, full, out error))
            {
                error = $"{Path.GetFileName(full)}: {error}";
                return false;
            }

            return true;
        }

        private static FileSystemWatcher? CreateWatcher(WatchedFile file)
        {
            try
            {
                var watcher = new FileSystemWatcher(Path.GetDirectoryName(file.Path)!, Path.GetFileName(file.Path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime
                };
                FileSystemEventHandler changed = (_, _) =>
                {
                    lock (Sync)
                    {
                        file.ChangedAt = DateTime.UtcNow;
                    }
                };
                watcher.Changed += changed;
                watcher.Created += changed;
                watcher.Renamed += (_, _) =>
                {
                    lock (Sync)
                    {
                        file.ChangedAt = DateTime.UtcNow;
                    }
                };
                watcher.EnableRaisingEvents = true;
                UIServices.Log($"[{file.Owner}] watching {file.Path} for changes.", LogLevel.Debug);
                return watcher;
            }
            catch (Exception ex)
            {
                UIServices.Log($"[{file.Owner}] could not watch {file.Path}: {ex.Message}", LogLevel.Warn);
                return null;
            }
        }

        /// <summary>Re-import watched files that changed (debounced); called every update tick on the main thread.</summary>
        internal static void Poll()
        {
            List<WatchedFile>? due = null;
            lock (Sync)
            {
                if (Files.Count == 0)
                {
                    return;
                }

                DateTime now = DateTime.UtcNow;
                foreach (WatchedFile file in Files.Values)
                {
                    if (file.ChangedAt is { } at && (now - at).TotalMilliseconds >= DebounceMs)
                    {
                        file.ChangedAt = null;
                        (due ??= new List<WatchedFile>()).Add(file);
                    }
                }
            }

            if (due == null)
            {
                return;
            }

            foreach (WatchedFile file in due)
            {
                if (ReadFile(file.Owner, file.Path, out string error))
                {
                    UIServices.Log($"[{file.Owner}] re-imported {Path.GetFileName(file.Path)}.", LogLevel.Info);
                }
                else
                {
                    UIServices.Log($"[{file.Owner}] {error}", LogLevel.Warn);
                }
            }
        }

        /// <summary>Re-read every imported file now (<c>ui_reload</c>); returns the number of files read.</summary>
        internal static int ReloadFiles()
        {
            WatchedFile[] files;
            lock (Sync)
            {
                files = Files.Values.ToArray();
            }

            int count = 0;
            foreach (WatchedFile file in files)
            {
                if (ReadFile(file.Owner, file.Path, out string error))
                {
                    count++;
                }
                else
                {
                    UIServices.Log($"[{file.Owner}] {error}", LogLevel.Warn);
                }
            }

            return count;
        }

        /// <summary>The imported layers for <c>ui_data</c>.</summary>
        internal static IEnumerable<string> Describe()
        {
            lock (Sync)
            {
                return Layers.Select(l => $"{l.Owner}: {(l.Source == "inline" ? "ImportData" : l.Source)} ({l.Menus.Count} menu(s), {l.Huds.Count} HUD(s), {l.Sprites.Count} sprite(s), {l.Composites.Count} composite(s), {l.Contributions.Count} contribution(s){(l.Owners.Count > 0 ? ", owner settings" : string.Empty)}){(Files.TryGetValue(l.Source, out WatchedFile? f) && f.Watcher != null ? " [watched]" : string.Empty)}").ToArray();
            }
        }
    }
}
