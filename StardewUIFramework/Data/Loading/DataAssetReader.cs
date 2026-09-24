using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using UIFramework.Data.Model;

namespace UIFramework.Data.Loading
{
    /// <summary>
    /// Reads the data assets through the content pipeline (so Content Patcher edits apply) and returns private deep
    /// copies: the cached asset instances are never mutated by normalization. Entries with a <c>From</c> are merged
    /// over the standalone file they name (the entry's own members win).
    /// </summary>
    internal sealed class DataAssetReader
    {
        /// <summary>Serializer settings for copies, merges and hashes (nulls omitted so a merge never erases a member).</summary>
        internal static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        private static readonly JsonMergeSettings MergeSettings = new()
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            MergeNullValueHandling = MergeNullValueHandling.Ignore
        };

        private readonly IGameContentHelper content;

        internal DataAssetReader(IGameContentHelper content)
        {
            this.content = content;
        }

        /// <summary>The standalone definition assets read by the last <see cref="ReadMenus"/> (their invalidation triggers a reload).</summary>
        internal HashSet<string> FromAssets { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every menu entry (copied, <c>From</c> merged). Problems are added to <paramref name="logs"/> by key; a failed entry is left out.</summary>
        internal Dictionary<string, MenuDefinition> ReadMenus(Dictionary<string, DataMessageLog> logs, DataMessageLog assetLog)
        {
            FromAssets.Clear();
            var result = new Dictionary<string, MenuDefinition>(StringComparer.Ordinal);
            Dictionary<string, MenuDefinition>? asset = Load<Dictionary<string, MenuDefinition>>(DataAssets.Menus, assetLog);
            if (asset == null)
            {
                return result;
            }

            foreach ((string key, MenuDefinition? entry) in asset)
            {
                if (string.IsNullOrWhiteSpace(key) || entry == null)
                {
                    continue;
                }

                var log = new DataMessageLog();
                logs[key] = log;
                DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Menus), key);
                try
                {
                    MenuDefinition? copy = string.IsNullOrWhiteSpace(entry.From) ? Clone(entry) : MergeFrom(entry, path, log);
                    if (copy != null)
                    {
                        result[key] = copy;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(path, $"could not be read: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>Every HUD entry (copied). Problems are added to <paramref name="logs"/> by key; a failed entry is left out.</summary>
        internal Dictionary<string, HudDefinition> ReadHuds(Dictionary<string, DataMessageLog> logs, DataMessageLog assetLog)
        {
            var result = new Dictionary<string, HudDefinition>(StringComparer.Ordinal);
            Dictionary<string, HudDefinition>? asset = Load<Dictionary<string, HudDefinition>>(DataAssets.Huds, assetLog);
            if (asset == null)
            {
                return result;
            }

            foreach ((string key, HudDefinition? entry) in asset)
            {
                if (string.IsNullOrWhiteSpace(key) || entry == null)
                {
                    continue;
                }

                var log = new DataMessageLog();
                logs[key] = log;
                try
                {
                    result[key] = Clone(entry);
                }
                catch (Exception ex)
                {
                    log.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key), $"could not be read: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>Every owner entry (copied), keyed by mod id.</summary>
        internal Dictionary<string, OwnerDefinition> ReadOwners(DataMessageLog log)
        {
            var result = new Dictionary<string, OwnerDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, OwnerDefinition>? asset = Load<Dictionary<string, OwnerDefinition>>(DataAssets.Owners, log);
            if (asset == null)
            {
                return result;
            }

            foreach ((string key, OwnerDefinition? entry) in asset)
            {
                if (!string.IsNullOrWhiteSpace(key) && entry != null)
                {
                    try
                    {
                        result[key.Trim()] = Clone(entry);
                    }
                    catch (Exception ex)
                    {
                        log.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Owners), key), $"could not be read: {ex.Message}");
                    }
                }
            }

            return result;
        }

        /// <summary>Every sprite entry (copied).</summary>
        internal Dictionary<string, SpriteDefinition> ReadSprites(DataMessageLog log)
        {
            var result = new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, SpriteDefinition>? asset = Load<Dictionary<string, SpriteDefinition>>(DataAssets.Sprites, log);
            if (asset == null)
            {
                return result;
            }

            foreach ((string key, SpriteDefinition? entry) in asset)
            {
                if (!string.IsNullOrWhiteSpace(key) && entry != null)
                {
                    result[key.Trim()] = Clone(entry);
                }
            }

            return result;
        }

        /// <summary>Every data composite entry (copied), keyed by global name (v1.7).</summary>
        internal Dictionary<string, DataCompositeDefinition> ReadComposites(DataMessageLog log) => ReadEntries<DataCompositeDefinition>(DataAssets.Composites, log);

        /// <summary>Every contribution entry (copied), keyed <c>&lt;contributor&gt;/&lt;name&gt;</c> (v1.7).</summary>
        internal Dictionary<string, ContributionDefinition> ReadContributions(DataMessageLog log) => ReadEntries<ContributionDefinition>(DataAssets.Contributions, log);

        private Dictionary<string, T> ReadEntries<T>(string assetName, DataMessageLog log) where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            Dictionary<string, T>? asset = Load<Dictionary<string, T>>(assetName, log);
            if (asset == null)
            {
                return result;
            }

            foreach ((string key, T? entry) in asset)
            {
                if (string.IsNullOrWhiteSpace(key) || entry == null)
                {
                    continue;
                }

                try
                {
                    result[key.Trim()] = Clone(entry);
                }
                catch (Exception ex)
                {
                    log.Error(DataPath.Entry(DataAssets.ShortName(assetName), key), $"could not be read: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>A deep copy through JSON (converters and extension data included).</summary>
        internal static T Clone<T>(T value) where T : class
        {
            JToken token = JToken.FromObject(value, Serializer);
            return token.ToObject<T>(Serializer)!;
        }

        /// <summary>The standalone file named by <c>From</c> with the entry's own members merged over it.</summary>
        private MenuDefinition? MergeFrom(MenuDefinition entry, DataPath path, DataMessageLog log)
        {
            string from = entry.From!.Trim();
            FromAssets.Add(from.Replace('\\', '/'));
            MenuDefinition? file = Load<MenuDefinition>(from, log, path.Field("From"));
            if (file == null)
            {
                return null;
            }

            JObject merged = JObject.FromObject(file, Serializer);
            JObject over = JObject.FromObject(entry, Serializer);
            over.Remove(nameof(MenuDefinition.From));
            merged.Remove(nameof(MenuDefinition.From));
            merged.Merge(over, MergeSettings);
            return merged.ToObject<MenuDefinition>(Serializer);
        }

        private T? Load<T>(string assetName, DataMessageLog log, DataPath? path = null) where T : class
        {
            try
            {
                return content.Load<T>(assetName);
            }
            catch (Exception ex)
            {
                string message = $"asset '{assetName}' could not be loaded: {ex.GetBaseException().Message}";
                if (path != null)
                {
                    log.Error(path, message);
                }
                else
                {
                    log.Error(DataPath.Entry(DataAssets.ShortName(assetName), "*"), message);
                }

                return null;
            }
        }
    }
}
