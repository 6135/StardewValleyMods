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
    /// <summary>Action lists, conditions, expressions, state keys, key bindings and state members.</summary>
    internal sealed partial class DataValidator
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Actions and conditions
        // ---------------------------------------------------------------------------------------------------------

        private static void CheckActions(List<ActionDefinition>? actions, DataPath path, DataMessageLog log)
        {
            if (actions == null)
            {
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                ActionDefinition entry = actions[i];
                DataPath entryPath = path.Index(i, null);
                if (entry == null)
                {
                    continue;
                }

                CheckUnknown(entry.Unknown, typeof(ActionDefinition), entryPath, log);
                if (string.IsNullOrWhiteSpace(entry.Action) && entry.Actions == null && entry.Else == null)
                {
                    log.Warn(entryPath, "the entry has no Action, Actions or Else; it does nothing.");
                }

                if (!string.IsNullOrWhiteSpace(entry.Action))
                {
                    if (entry.Action.TrimStart().StartsWith('@'))
                    {
                        // "@owner/command args": a C# command (may be registered after the data loads)
                        if (entry.Action.Trim().Length < 2)
                        {
                            log.Warn(entryPath.Field("Action"), "'@' needs a command name (@ModId/command).");
                        }
                    }
                    else if (ExpressionValueResolver.HasTemplate(entry.Action))
                    {
                        CheckTemplate(entry.Action, entryPath.Field("Action"), log);
                    }
                    else if (!TriggerActionManager.TryValidateActionExists(entry.Action.Trim(), out string error))
                    {
                        log.Warn(entryPath.Field("Action"), error);
                    }
                    else
                    {
                        // a known action
                    }
                }

                CheckCondition(entry.Condition, entryPath.Field("Condition"), log);
                CheckExpression(entry.When, entryPath.Field("When"), log);

                CheckActions(entry.Actions, entryPath.Field("Actions"), log);
                CheckActions(entry.Else, entryPath.Field("Else"), log);
            }
        }

        private static void CheckCondition(string? condition, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return;
            }

            foreach (GameStateQuery.ParsedGameStateQuery query in GameStateQuery.Parse(condition))
            {
                if (query.Error != null)
                {
                    log.Warn(path, $"invalid game state query '{condition}': {query.Error}");
                    return;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Expressions and state keys (v1.4)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Compile-check every <c>${...}</c> / <c>$:{...}</c> in the string members of a definition.</summary>
        private static void CheckTemplates(object def, DataPath path, DataMessageLog log)
        {
            foreach (PropertyInfo property in ModelProperties(def.GetType()))
            {
                if (property.PropertyType == typeof(string) && property.GetValue(def) is string raw
                    && property.Name is not (nameof(ElementDefinition.If) or nameof(ElementDefinition.Switch) or nameof(ElementDefinition.Validate) or nameof(HudDefinition.ShowWhen)
                        or nameof(ElementDefinition.Filter) or nameof(ColumnDefinition.SortNumber) or nameof(ColumnDefinition.SortKey) or nameof(TooltipBlockDefinition.When)))
                {
                    CheckTemplate(raw, path.Field(property.Name), log);
                }
            }
        }

        /// <summary>Report the parse errors of a text value's <c>${...}</c> segments.</summary>
        private static void CheckTemplate(string raw, DataPath path, DataMessageLog log)
        {
            if (!ExpressionValueResolver.HasTemplate(raw))
            {
                return;
            }

            foreach (ParseError error in Template.Parse(raw).Errors)
            {
                log.Error(path, $"expression error in '{raw}': {error}");
            }
        }

        /// <summary>Report the parse errors of a bare expression field (or a template).</summary>
        private static void CheckExpression(string? raw, DataPath path, DataMessageLog log)
        {
            if (raw == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                log.Warn(path, "the expression is empty.");
                return;
            }

            foreach (ParseError error in Template.ParseField(raw).Errors)
            {
                log.Error(path, $"expression error in '{raw}': {error}");
            }
        }

        /// <summary>Check the syntax of a state key (<c>Bind</c>, <c>Out</c>); owners and menus are resolved at build time.</summary>
        private static void CheckStateKey(string? raw, DataPath path, DataMessageLog log)
        {
            if (raw == null)
            {
                return;
            }

            string text = raw.Trim();
            if (text.StartsWith('.'))
            {
                text = "menu" + text; // relative to a With: only the syntax can be checked here
            }

            if (!StateAddress.TryParse(text, DataScope.ForMenu("owner", "menu", null), allowBare: true, out _, out string error))
            {
                log.Error(path, error);
            }
        }

        /// <summary>Check an input's Bind: a state key, or (v1.6) a model / exposed value path (model.x.y, @owner/name).</summary>
        private void CheckBindKey(string? raw, DataPath path, DataMessageLog log)
        {
            if (raw == null)
            {
                return;
            }

            if (raw.Trim().StartsWith("args.", StringComparison.Ordinal))
            {
                if (bodyDepth == 0)
                {
                    log.Warn(path, $"'{raw}' binds to an argument, which only exists inside a template or data composite body.");
                }

                return; // a two-way argument of the enclosing template / composite (checked when built)
            }

            if (Bridge.BindTarget.IsModelSyntax(raw))
            {
                CompiledExpression expression = CompiledExpression.Compile(raw.Trim());
                if (expression.Root is not PathNode { StaticPath: not null })
                {
                    log.Error(path, $"'{raw}' is not a model path such as model.settings.Day{(expression.Error != null ? $" ({expression.Error})" : string.Empty)}.");
                }

                return;
            }

            CheckStateKey(raw, path, log);
        }

        private static void CheckKeys(List<KeyBindingDefinition>? keys, DataPath path, DataMessageLog log)
        {
            if (keys == null)
            {
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                KeyBindingDefinition? entry = keys[i];
                DataPath entryPath = path.Index(i, null);
                if (entry == null)
                {
                    continue;
                }

                CheckUnknown(entry.Unknown, typeof(KeyBindingDefinition), entryPath, log);
                if (!DataBuilder.TryParseKey(entry.Key, out _))
                {
                    log.Error(entryPath.Field("Key"), $"'{entry.Key}' is not a key name (e.g. Enter, Delete, F5, A).");
                }

                CheckActions(entry.Actions, entryPath.Field("Actions"), log);
            }
        }

        /// <summary>Check a UI's <c>State</c>, <c>Computed</c> and <c>Watch</c> members.</summary>
        private static void CheckStateMembers(Dictionary<string, string>? state, Dictionary<string, string>? computed, Dictionary<string, List<ActionDefinition>>? watch, DataScope context, DataPath path, DataMessageLog log)
        {
            if (state != null)
            {
                foreach ((string name, string? raw) in state)
                {
                    if (raw != null)
                    {
                        CheckTemplate(raw, path.Field("State").Field(name), log);
                    }
                }
            }

            if (computed != null)
            {
                foreach ((string name, string? raw) in computed)
                {
                    CheckExpression(raw, path.Field("Computed").Field(name), log);
                    if (state != null && state.ContainsKey(name))
                    {
                        log.Warn(path.Field("Computed").Field(name), $"'{name}' is both a State value and a Computed value; the computed value wins.");
                    }
                }
            }

            if (watch != null)
            {
                foreach ((string key, List<ActionDefinition>? actions) in watch)
                {
                    if (!StateAddress.TryParse(key, context, allowBare: true, out _, out string error))
                    {
                        log.Error(path.Field("Watch").Field(key), error);
                    }

                    CheckActions(actions, path.Field("Watch").Field(key), log);
                }
            }
        }
    }
}
