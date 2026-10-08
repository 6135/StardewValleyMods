using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Data
{
    /// <summary>Data HUDs: reloading, building and showing / hiding them.</summary>
    internal sealed partial class DataService
    {
        private int ReloadHuds(Dictionary<string, HudDefinition> entries, Dictionary<string, DataMessageLog> logs)
        {
            int changed = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string key, DataMessageLog readLog) in logs)
            {
                string statusKey = "hud:" + key;
                var status = new EntryStatus { Key = statusKey, Messages = readLog };
                statuses[statusKey] = status;
                if (!entries.TryGetValue(key, out HudDefinition? def))
                {
                    status.State = "not readable";
                    Report(statusKey, string.Empty, readLog);
                    continue;
                }

                try
                {
                    if (ApplyHud(key, def, status, seen))
                    {
                        changed++;
                    }
                }
                catch (Exception ex)
                {
                    status.Messages.Error(DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key), $"could not be built: {ex.Message}");
                    status.State = "failed";
                    monitor.Log($"Data HUD '{key}' failed to build:\n{ex}", LogLevel.Trace);
                }

                Report(statusKey, status.Hash + "|" + status.State.Replace("rebuilt", "built"), status.Messages);
            }

            foreach (string key in huds.Keys.Where(k => !seen.Contains(k)).ToArray())
            {
                huds.Remove(key, out DataHudRuntime? runtime);
                if (runtime != null)
                {
                    StardewUIApi api = FacadeFor(runtime.Owner);
                    api.UnregisterHotkey(HudBuilder.HotkeyId(runtime.Id));
                    api.DestroyHud(runtime.Id);
                    monitor.Log($"Data HUD '{key}' was removed from {DataAssets.Huds}.", LogLevel.Trace);
                }
            }

            return changed;
        }

        private bool ApplyHud(string key, HudDefinition def, EntryStatus status, HashSet<string> seen)
        {
            if (!validator.ValidateHud(key, def, status.Messages, out string owner, out string hudId))
            {
                status.State = "rejected";
                return false;
            }

            string runtimeKey = owner + "/" + hudId;
            string hash = DefinitionHash.Of(def, sprites.Hash, OwnerHash(owner), HooksHash);
            status.Hash = hash;
            seen.Add(runtimeKey);
            huds.TryGetValue(runtimeKey, out DataHudRuntime? runtime);
            StardewUIApi api = FacadeFor(owner);
            UIHud? existing = UIServices.Hud?.Get(owner, hudId);
            if (existing != null && (runtime == null || runtime.Hud != existing))
            {
                status.Messages.Warn(DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key), $"'{owner}' already created HUD '{hudId}' in C#; the C# widget wins and this entry is skipped.");
                status.State = "skipped (C# HUD)";
                huds.Remove(runtimeKey);
                seen.Remove(runtimeKey);
                return false;
            }

            // the widget is no longer registered (a C# DestroyHud): create a new one, whatever the hash
            if (runtime?.Hud != null && existing == null)
            {
                runtime.Hud = null;
            }
            else if (runtime != null && runtime.Hash == hash)
            {
                status.Messages = runtime.Messages;
                status.State = "built";
                return false;
            }

            bool rebuild = runtime?.Hud != null;
            runtime ??= new DataHudRuntime(owner, hudId);
            runtime.Definition = def;
            runtime.Hash = string.Empty; // set once the build succeeded: a failed build is retried on the next reload
            runtime.Messages = status.Messages;
            huds[runtimeKey] = runtime;
            hudBuilder.Build(api, runtime);
            runtime.Hash = hash;
            status.State = rebuild ? "rebuilt" : "built";
            return true;
        }

        /// <summary>Show (true), hide (false) or toggle (null) a data HUD for the current player.</summary>
        internal bool SetHudVisible(string key, bool? visible, out string error)
        {
            error = string.Empty;
            if (!huds.TryGetValue(key.Trim(), out DataHudRuntime? runtime))
            {
                error = $"no data HUD '{key}' is loaded (expected '<owner>/<hud id>' from {DataAssets.Huds}).";
                return false;
            }

            runtime.Visible = visible ?? !runtime.Visible;
            return true;
        }

        /// <summary>True when a data HUD is shown for the current player (its visibility and ShowWhen).</summary>
        internal bool IsHudShown(string key)
        {
            if (!huds.TryGetValue(key.Trim(), out DataHudRuntime? runtime) || runtime.Hud == null)
            {
                return false;
            }

            return runtime.Visible && runtime.Hud.ShowWhen?.Invoke() != false;
        }
    }
}
