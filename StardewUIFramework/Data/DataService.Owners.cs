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
    /// <summary>The <c>Owners</c> asset: each owner's tooltip delay, default style and hotkeys.</summary>
    internal sealed partial class DataService
    {
        /// <summary>Read the <c>Owners</c> asset and apply each owner's tooltip delay, default style and hotkeys.</summary>
        private void ReloadOwners(DataMessageLog log)
        {
            Dictionary<string, OwnerDefinition> definitions = reader.ReadOwners(log);
            var valid = new Dictionary<string, OwnerDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach ((string owner, OwnerDefinition def) in definitions)
            {
                if (validator.ValidateOwner(owner, def, log))
                {
                    valid[owner] = def;
                }
            }

            // owners that were removed go back to their C# values
            foreach (string owner in owners.Keys.Where(o => !valid.ContainsKey(o)).ToArray())
            {
                ApplyOwner(owner, null, log);
                owners.Remove(owner);
                ownerHashes.Remove(owner);
            }

            foreach ((string owner, OwnerDefinition def) in valid)
            {
                string hash = DefinitionHash.Of(def);
                if (ownerHashes.TryGetValue(owner, out string? previous) && previous == hash)
                {
                    continue;
                }

                owners[owner] = def;
                ownerHashes[owner] = hash;
                ApplyOwner(owner, def, log);
            }
        }

        private string OwnerHash(string owner) => ownerHashes.TryGetValue(owner, out string? hash) ? hash : string.Empty;

        private void ApplyOwner(string owner, OwnerDefinition? def, DataMessageLog log)
        {
            ConsumerContext context = contexts.For(owner);
            StardewUIApi api = FacadeFor(owner);
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Owners), owner);
            DataScope scope = DataScope.ForOwner(owner);

            // only the fields the entry sets are applied; the C# values (SetTooltipDelay / SetDefaultStyle) are captured
            // before data first overrides them and restored when the entry stops setting them (or is removed)
            if (!ownerOverrides.TryGetValue(owner, out OwnerOverrides? overrides))
            {
                ownerOverrides[owner] = overrides = new OwnerOverrides();
            }

            if (def?.TooltipDelayMs != null && ValueParsers.Int.Parse(def.TooltipDelayMs, out int delay) && delay >= 0)
            {
                if (!overrides.DelaySet)
                {
                    overrides.Delay = context.TooltipDelayMs;
                    overrides.DelaySet = true;
                }

                context.TooltipDelayMs = delay;
            }
            else if (overrides.DelaySet)
            {
                context.TooltipDelayMs = overrides.Delay;
                overrides.DelaySet = false;
            }

            if (def?.DefaultStyle != null)
            {
                if (!overrides.StyleSet)
                {
                    overrides.Style = context.DefaultStyle;
                    overrides.StyleSet = true;
                }

                // the owner style is applied once: live values are evaluated now and do not update
                var group = new RefresherGroup(owner, "DefaultStyle");
                var applier = new PropertyApplier(Resolver, group, log);
                api.SetDefaultStyle(builder.BuildStyle(api, def.DefaultStyle, scope, path.Field("DefaultStyle"), applier));
                if (group.Count > 0)
                {
                    log.Warn(path.Field("DefaultStyle"), "DefaultStyle takes literal values only: its ${...} values were evaluated once and do not update.");
                }
            }
            else if (overrides.StyleSet)
            {
                context.DefaultStyle = overrides.Style;
                overrides.StyleSet = false;
            }

            if (ownerHotkeys.TryGetValue(owner, out List<string>? previous))
            {
                foreach (string id in previous)
                {
                    api.UnregisterHotkey(id);
                }
            }

            var registered = new List<string>();
            if (def?.Hotkeys != null)
            {
                foreach ((string id, HotkeyDefinition? hotkey) in def.Hotkeys)
                {
                    if (hotkey?.Keys == null || !ValueParsers.Keybind.Parse(hotkey.Keys, out _))
                    {
                        continue;
                    }

                    string hotkeyId = "data:" + id;
                    HotkeyDefinition current = hotkey;
                    api.RegisterHotkey(hotkeyId, hotkey.Keys, () =>
                    {
                        if (DataActionRunner.CheckCondition(current.Condition))
                        {
                            DataActionRunner.Run(current.Actions, scope.WithEvent("Hotkey", null));
                        }
                    });
                    registered.Add(hotkeyId);
                }
            }

            ownerHotkeys[owner] = registered;
        }

        /// <summary>The owner's C# values an <c>Owners</c> entry replaced, restored when the entry stops setting them.</summary>
        private sealed class OwnerOverrides
        {
            internal bool DelaySet;
            internal int? Delay;
            internal bool StyleSet;
            internal UIStyle? Style;
        }
    }
}
