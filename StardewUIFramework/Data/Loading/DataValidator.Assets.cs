using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Triggers;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Rendering;

namespace UIFramework.Data.Loading
{
    /// <summary>The HUDs, Owners and Sprites assets.</summary>
    internal sealed partial class DataValidator
    {
        // ---------------------------------------------------------------------------------------------------------
        //  HUDs and owners (v1.4)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Validate and normalize a HUD entry; false when it must be rejected (bad key, owner not loaded).</summary>
        internal bool ValidateHud(string key, HudDefinition def, DataMessageLog log, out string owner, out string hudId)
        {
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Huds), key);
            if (!TrySplitKey(key, out owner, out hudId))
            {
                log.Error(path, "the key must be '<owner mod id>/<hud id>'; the entry is skipped.");
                return false;
            }

            if (!isLoaded(owner))
            {
                log.Error(path, $"the owner '{owner}' is not a loaded mod or content pack; the entry is skipped.");
                return false;
            }

            CheckUnknown(def.Unknown, typeof(HudDefinition), path, log);
            if (def.Hotkey != null && !ValueParsers.Keybind.Parse(def.Hotkey, out _))
            {
                log.Warn(path.Field("Hotkey"), $"'{def.Hotkey}' is not {ValueParsers.Keybind.Description}; no hotkey is bound.");
            }

            CheckTemplates(def, path, log);
            templates = TemplateLookup(null, owner);
            CheckExpression(def.ShowWhen, path.Field("ShowWhen"), log);
            CheckActions(def.OnUpdate, path.Field("OnUpdate"), log);
            CheckStateMembers(def.State, def.Computed, def.Watch, DataScope.ForMenu(owner, "hud:" + hudId, null), path, log);
            sourceNames = null;
            CheckSources(def.Sources, owner, path.Field("Sources"), log);
            CheckChildren(def.Children, hudId, owner, path.Field("Children"), new Dictionary<string, string>(StringComparer.Ordinal), log);
            return true;
        }

        /// <summary>Validate an owner entry; false when it must be ignored (owner not loaded).</summary>
        internal bool ValidateOwner(string owner, OwnerDefinition def, DataMessageLog log)
        {
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Owners), owner);
            if (!isLoaded(owner))
            {
                log.Error(path, $"'{owner}' is not a loaded mod or content pack; the entry is ignored.");
                return false;
            }

            CheckUnknown(def.Unknown, typeof(OwnerDefinition), path, log);
            if (def.TooltipDelayMs != null && !ValueParsers.Int.Parse(def.TooltipDelayMs, out _))
            {
                log.Warn(path.Field("TooltipDelayMs"), $"'{def.TooltipDelayMs}' is not a whole number.");
            }

            if (def.DefaultStyle != null)
            {
                CheckUnknown(def.DefaultStyle.Unknown, typeof(StyleDefinition), path.Field("DefaultStyle"), log);
            }

            if (def.Classes != null)
            {
                foreach ((string name, StyleDefinition? style) in def.Classes)
                {
                    if (style != null)
                    {
                        CheckUnknown(style.Unknown, typeof(StyleDefinition), path.Field("Classes").Field(name), log);
                        CheckSprite(style.BoxTexture, owner, path.Field("Classes").Field(name).Field("BoxTexture"), log);
                    }
                }
            }

            if (def.Hotkeys != null)
            {
                foreach ((string id, HotkeyDefinition? hotkey) in def.Hotkeys)
                {
                    DataPath hotkeyPath = path.Field("Hotkeys").Field(id);
                    if (hotkey == null)
                    {
                        continue;
                    }

                    CheckUnknown(hotkey.Unknown, typeof(HotkeyDefinition), hotkeyPath, log);
                    if (hotkey.Keys == null || !ValueParsers.Keybind.Parse(hotkey.Keys, out _))
                    {
                        log.Warn(hotkeyPath.Field("Keys"), $"'{hotkey.Keys}' is not {ValueParsers.Keybind.Description}; the hotkey is not bound.");
                    }

                    CheckCondition(hotkey.Condition, hotkeyPath.Field("Condition"), log);
                    CheckActions(hotkey.Actions, hotkeyPath.Field("Actions"), log);
                }
            }

            if (def.Tooltips != null)
            {
                foreach ((string name, TooltipDefinition? tooltip) in def.Tooltips)
                {
                    if (tooltip?.From != null)
                    {
                        log.Warn(path.Field("Tooltips").Field(name).Field("From"), "a named tooltip cannot start from another one; From is ignored here.");
                    }

                    CheckTooltip(tooltip, owner, path.Field("Tooltips").Field(name), log);
                }
            }

            templates = TemplateLookup(def.Templates, owner);
            sourceNames = null;
            CheckTemplateDefinitions(def.Templates, owner, path.Field("Templates"), log);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Sprites
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Validate the sprites asset (problems never stop menus from building; a bad sprite fails where it is used).</summary>
        internal void ValidateSprites(Dictionary<string, SpriteDefinition> definitions, DataMessageLog log)
        {
            foreach ((string key, SpriteDefinition def) in definitions)
            {
                DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Sprites), key);
                if (!TrySplitKey(key, out string owner, out _))
                {
                    log.Warn(path, "the key should be '<owner mod id>/<name>'.");
                }
                else if (!isLoaded(owner))
                {
                    log.Warn(path, $"the owner '{owner}' is not a loaded mod or content pack.");
                }
                else
                {
                    // a valid owner
                }

                CheckUnknown(def.Unknown, typeof(SpriteDefinition), path, log);
                if (string.IsNullOrWhiteSpace(def.Texture))
                {
                    log.Error(path.Field("Texture"), "a sprite needs a Texture.");
                }

                if (def.Source != null && ThemeData.ParseRectangle(def.Source) == null)
                {
                    log.Warn(path.Field("Source"), $"'{def.Source}' is not a rectangle 'x,y,width,height'.");
                }

                if (def.Border != null && ThemeData.ParseRectangle(def.Border) == null)
                {
                    log.Warn(path.Field("Border"), $"'{def.Border}' is not a rectangle 'x,y,width,height'.");
                }

                if (def.Scale != null && !ValueParsers.Number.Parse(def.Scale, out _))
                {
                    log.Warn(path.Field("Scale"), $"'{def.Scale}' is not a number.");
                }

                if (def.Tint != null && !ValueParsers.ColorValue.Parse(def.Tint, out _))
                {
                    log.Warn(path.Field("Tint"), $"'{def.Tint}' is not {ValueParsers.ColorValue.Description}.");
                }
            }
        }
    }
}
