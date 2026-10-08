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
    /// <summary>Templates, composites and contributions (v1.7).</summary>
    internal sealed partial class DataValidator
    {
        /// <summary>The parameter types a template / composite may declare.</summary>
        private static readonly string[] ParamTypes = { "string", "number", "bool", "any" };

        /// <summary>Check a set of templates (a menu's or an owner's): parameters and bodies (whose ids are prefixed with the instance's at build time).</summary>
        private void CheckTemplateDefinitions(Dictionary<string, TemplateDefinition>? definitions, string owner, DataPath path, DataMessageLog log)
        {
            if (definitions == null)
            {
                return;
            }

            foreach ((string name, TemplateDefinition? template) in definitions)
            {
                DataPath templatePath = path.Field(name);
                if (template == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(name) || name.Contains('.') || ElementTypes.Canonical(name) != null)
                {
                    log.Error(templatePath, $"'{name}' cannot name a template: it must not be empty, contain a dot (custom tags) or be a built-in type.");
                    continue;
                }

                CheckTemplateBody(template, typeof(TemplateDefinition), name, owner, templatePath, log);
            }
        }

        /// <summary>Check the members shared by templates and data composites: parameters, layout and the body.</summary>
        private void CheckTemplateBody(TemplateDefinition def, Type model, string name, string owner, DataPath path, DataMessageLog log)
        {
            CheckUnknown(def.Unknown, model, path, log);
            CheckParams(def.Params, path.Field("Params"), log);
            if (def.Horizontal != null && !ValueParsers.Bool.Parse(def.Horizontal, out _))
            {
                log.Warn(path.Field("Horizontal"), $"'{def.Horizontal}' is not true or false.");
            }

            if (def.Spacing != null && !ValueParsers.Int.Parse(def.Spacing, out _))
            {
                log.Warn(path.Field("Spacing"), $"'{def.Spacing}' is not a whole number.");
            }

            if (def.Children == null || def.Children.Count == 0)
            {
                log.Warn(path.Field("Children"), $"'{name}' has no body (Children); its instances are empty.");
                return;
            }

            bodyDepth++;
            try
            {
                var types = new Dictionary<string, string>(StringComparer.Ordinal);
                CheckChildren(def.Children, name, owner, path.Field("Children"), types, log);
                int defaultOutlets = CountOutlets(def.Children, string.Empty);
                if (defaultOutlets > 1)
                {
                    log.Warn(path.Field("Children"), $"'{name}' has {defaultOutlets} default Outlets; the instance's children go into the first one.");
                }
            }
            finally
            {
                bodyDepth--;
            }
        }

        private static int CountOutlets(List<ElementDefinition>? children, string name)
        {
            if (children == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ElementDefinition child in children)
            {
                if (child == null)
                {
                    continue;
                }

                string outlet = child.Outlet?.Trim() ?? string.Empty;
                if (child.Type == ElementTypes.Outlet && (outlet.Equals("default", StringComparison.OrdinalIgnoreCase) ? string.Empty : outlet) == name)
                {
                    count++;
                }

                if (child.Type != ElementTypes.Template && child.Type != ElementTypes.Composite)
                {
                    count += CountOutlets(child.Children, name);
                }
            }

            return count;
        }

        /// <summary>Check parameter declarations (type names, Required, a Default that fits the type).</summary>
        private static void CheckParams(Dictionary<string, ParamDefinition>? parameters, DataPath path, DataMessageLog log)
        {
            if (parameters == null)
            {
                return;
            }

            foreach ((string name, ParamDefinition? param) in parameters)
            {
                DataPath paramPath = path.Field(name);
                if (param == null)
                {
                    continue;
                }

                CheckUnknown(param.Unknown, typeof(ParamDefinition), paramPath, log);
                if (!IsIdentifier(name.Trim()))
                {
                    log.Error(paramPath, $"'{name}' is not a parameter name (letters, digits and _ , not starting with a digit); args.{name} cannot read it.");
                }

                if (param.Type != null && !ParamTypes.Contains(param.Type.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    string? suggestion = Suggest(param.Type, ParamTypes);
                    log.Warn(paramPath.Field("Type"), $"'{param.Type}' is not string, number, bool or any{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; the value is used as is.");
                }

                if (param.Required != null && !ValueParsers.Bool.Parse(param.Required, out _))
                {
                    log.Warn(paramPath.Field("Required"), $"'{param.Required}' is not true or false.");
                }

                if (param.Default != null && TemplateArgs.IsRequired(param))
                {
                    log.Info(paramPath.Field("Default"), "a Required parameter's Default is never used.");
                }

                if (param.Default != null && !LiteralFits(param.Default, param.Type))
                {
                    log.Warn(paramPath.Field("Default"), $"'{param.Default}' is not a {param.Type}.");
                }
            }
        }

        /// <summary>Check an instance's arguments against the parameters: required ones present, unknown ones reported, literals of the right type.</summary>
        private static void CheckArgs(string kind, string name, Dictionary<string, ParamDefinition>? parameters, Dictionary<string, JToken?>? args, DataPath path, DataMessageLog log)
        {
            if (parameters == null)
            {
                return;
            }

            foreach ((string param, ParamDefinition? definition) in parameters)
            {
                if (definition != null && TemplateArgs.IsRequired(definition) && (args == null || !args.Keys.Any(k => string.Equals(k.Trim(), param.Trim(), StringComparison.OrdinalIgnoreCase))))
                {
                    log.Error(path, $"{kind} '{name}' needs the argument '{param}' (Required); the body reads it as empty.");
                }
            }

            if (args == null)
            {
                return;
            }

            foreach ((string arg, JToken? value) in args)
            {
                ParamDefinition? definition = parameters.FirstOrDefault(p => string.Equals(p.Key.Trim(), arg.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
                if (definition == null)
                {
                    string? suggestion = Suggest(arg, parameters.Keys);
                    log.Warn(path.Field(arg), $"{kind} '{name}' has no parameter '{arg}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    continue;
                }

                if (value != null && !LiteralFits(value, definition.Type))
                {
                    log.Warn(path.Field(arg), $"'{value}' is not a {definition.Type} ({kind} '{name}' parameter '{arg}').");
                }
            }
        }

        /// <summary>True when a literal argument fits a parameter type (text with ${...} or a state reference always fits).</summary>
        private static bool LiteralFits(JToken value, string? type)
        {
            string kind = type?.Trim().ToLowerInvariant() ?? "any";
            if (value.Type is JTokenType.Null or JTokenType.Undefined)
            {
                return true;
            }

            if (value.Type == JTokenType.String)
            {
                string text = value.Value<string>() ?? string.Empty;
                if (ExpressionValueResolver.HasTemplate(text) || IsReferenceLike(text))
                {
                    return true;
                }

                return kind switch
                {
                    "number" => ValueParsers.Number.Parse(text, out _),
                    "bool" => ValueParsers.Bool.Parse(text, out _),
                    _ => true
                };
            }

            return kind switch
            {
                "number" => value.Type is JTokenType.Integer or JTokenType.Float,
                "bool" => value.Type == JTokenType.Boolean,
                _ => true
            };
        }

        /// <summary>Text that looks like a state / model / argument path (menu.x, config[owner].x, model.x, args.x, @owner/x).</summary>
        private static bool IsReferenceLike(string text)
        {
            string trimmed = text.Trim();
            int dot = trimmed.IndexOf('.');
            int bracket = trimmed.IndexOf('[');
            int end = dot < 0 ? bracket : bracket < 0 ? dot : Math.Min(dot, bracket);
            string root = end > 0 ? trimmed.Substring(0, end) : trimmed;
            return trimmed.StartsWith('@') || root is "menu" or "session" or "player" or "stat" or "config" or "model" or "args";
        }

        /// <summary>A menu's <c>Expose</c> expressions and <c>Commands</c> action lists.</summary>
        private static void CheckExposures(Dictionary<string, string>? expose, Dictionary<string, List<ActionDefinition>>? commands, DataPath path, DataMessageLog log)
        {
            if (expose != null)
            {
                foreach ((string key, string? raw) in expose)
                {
                    CheckExpression(raw, path.Field("Expose").Field(key), log);
                }
            }

            if (commands != null)
            {
                foreach ((string key, List<ActionDefinition>? actions) in commands)
                {
                    CheckActions(actions, path.Field("Commands").Field(key), log);
                }
            }
        }

        /// <summary>
        /// Validate and normalize a <c>Composites</c> entry; false when it must be rejected. The owner is the entry's
        /// <c>Owner</c> (a loaded mod), else the longest loaded mod id the name starts with (<c>Owner.Name</c>).
        /// </summary>
        internal bool ValidateComposite(string name, DataCompositeDefinition def, DataMessageLog log, out string owner)
        {
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Composites), name);
            owner = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                log.Error(path, "a composite needs a name; the entry is skipped.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(def.Owner))
            {
                owner = def.Owner.Trim();
                if (!isLoaded(owner))
                {
                    log.Error(path.Field("Owner"), $"the owner '{owner}' is not a loaded mod or content pack; the entry is skipped.");
                    return false;
                }
            }
            else
            {
                string[] parts = name.Trim().Split('.');
                for (int count = parts.Length - 1; count >= 1 && owner.Length == 0; count--)
                {
                    string prefix = string.Join(".", parts, 0, count);
                    if (isLoaded(prefix))
                    {
                        owner = prefix;
                    }
                }

                if (owner.Length == 0)
                {
                    log.Error(path, $"'{name}' does not start with a loaded mod id ('<ModId>.<Name>') and has no Owner; the entry is skipped.");
                    return false;
                }
            }

            if (!name.Contains('.'))
            {
                log.Warn(path, $"'{name}' has no dot, so it cannot be used as a custom tag (\"Type\": \"{name}\"); use \"Type\": \"Composite\", \"Composite\": \"{name}\".");
            }

            templates = TemplateLookup(null, owner);
            sourceNames = null;
            CheckTemplateBody(def, typeof(DataCompositeDefinition), name, owner, path, log);
            CheckExposures(def.Expose, def.Commands, path, log);
            return true;
        }

        /// <summary>Validate and normalize a <c>Contributions</c> entry; false when it must be rejected (bad key or target, contributor not loaded).</summary>
        internal bool ValidateContribution(string key, ContributionDefinition def, DataMessageLog log, out string contributor, out string name, out string targetOwner, out string targetMenu, out int priority)
        {
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Contributions), key);
            targetOwner = targetMenu = string.Empty;
            priority = 0;
            if (!TrySplitKey(key, out contributor, out name))
            {
                log.Error(path, "the key must be '<contributor mod id>/<name>'; the entry is skipped.");
                return false;
            }

            if (!isLoaded(contributor))
            {
                log.Error(path, $"the contributor '{contributor}' is not a loaded mod or content pack; the entry is skipped.");
                return false;
            }

            CheckUnknown(def.Unknown, typeof(ContributionDefinition), path, log);
            if (def.Target == null || !TrySplitKey(def.Target, out targetOwner, out targetMenu))
            {
                log.Error(path.Field("Target"), "the Target must be '<owner mod id>/<menu id>'; the entry is skipped.");
                return false;
            }

            if (!isLoaded(targetOwner))
            {
                log.Info(path.Field("Target"), $"'{targetOwner}' is not loaded; the contribution waits for a menu that never opens.");
            }

            if (def.Priority != null && !ValueParsers.Int.Parse(def.Priority, out priority))
            {
                log.Warn(path.Field("Priority"), $"'{def.Priority}' is not a whole number; 0 is used.");
                priority = 0;
            }

            templates = TemplateLookup(null, contributor);
            sourceNames = null;
            bool hasSlot = !string.IsNullOrWhiteSpace(def.Slot);
            if (hasSlot && (def.Children == null || def.Children.Count == 0))
            {
                log.Warn(path.Field("Children"), "a Slot contribution needs Children; nothing is added to the slot.");
            }
            else if (!hasSlot && def.Children is { Count: > 0 })
            {
                log.Warn(path.Field("Slot"), "Children need a Slot (or a Decorate Append / Insert operation); they are not shown.");
            }
            else
            {
                // consistent
            }

            CheckChildren(def.Children, name, contributor, path.Field("Children"), new Dictionary<string, string>(StringComparer.Ordinal), log);
            if (def.On != null)
            {
                foreach ((string eventName, List<ActionDefinition>? actions) in def.On)
                {
                    CheckActions(actions, path.Field("On").Field(eventName), log);
                }
            }

            CheckDecorations(def.Decorate, name, contributor, path.Field("Decorate"), log);
            if (!hasSlot && (def.Decorate == null || def.Decorate.Count == 0) && (def.On == null || def.On.Count == 0))
            {
                log.Warn(path, "the contribution has no Slot, Decorate or On; it does nothing.");
            }

            return true;
        }

        private void CheckDecorations(List<DecorationOp>? ops, string name, string contributor, DataPath path, DataMessageLog log)
        {
            if (ops == null)
            {
                return;
            }

            for (int i = 0; i < ops.Count; i++)
            {
                DecorationOp? op = ops[i];
                DataPath opPath = path.Index(i, op?.Target);
                if (op == null)
                {
                    continue;
                }

                CheckDecoration(op, i, name, contributor, opPath, log);
            }
        }

        /// <summary>Check one decoration operation (kind, target, children, fields).</summary>
        private void CheckDecoration(DecorationOp op, int index, string name, string contributor, DataPath opPath, DataMessageLog log)
        {
            CheckUnknown(op.Unknown, typeof(DecorationOp), opPath, log);
            string? kind = DecorationOps.Canonical(op.Op);
            if (kind == null)
            {
                string? suggestion = Suggest(op.Op ?? string.Empty, DecorationOps.All);
                log.Error(opPath.Field("Op"), $"'{op.Op}' is not an operation ({string.Join(", ", DecorationOps.All)}){(suggestion != null ? $"; did you mean '{suggestion}'?" : string.Empty)}. It is skipped.");
                return;
            }

            op.Op = kind;
            if (string.IsNullOrWhiteSpace(op.Target))
            {
                log.Error(opPath.Field("Target"), $"{kind} needs a Target (an element id of the menu).");
            }

            if (DecorationOps.AddsChildren(kind))
            {
                if (op.Children == null || op.Children.Count == 0)
                {
                    log.Warn(opPath.Field("Children"), $"{kind} needs Children.");
                }

                CheckChildren(op.Children, name + "." + index, contributor, opPath.Field("Children"), new Dictionary<string, string>(StringComparer.Ordinal), log);
            }
            else if (op.Children != null)
            {
                log.Warn(opPath.Field("Children"), $"{kind} does not add Children; they are ignored.");
            }

            if (kind == DecorationOps.Set)
            {
                CheckSetFields(op, opPath, log);
            }

            if (kind == DecorationOps.Move && op.Before == null && op.After == null && op.Into == null)
            {
                log.Error(opPath, "Move needs Before, After or Into (an element id).");
            }
        }

        /// <summary>Check a <c>Set</c> operation's fields: settable members and their templates.</summary>
        private static void CheckSetFields(DecorationOp op, DataPath opPath, DataMessageLog log)
        {
            if (op.Fields == null || op.Fields.Count == 0)
            {
                log.Warn(opPath.Field("Fields"), "Set needs Fields ({ \"Text\": \"...\" }).");
            }
            else
            {
                foreach ((string field, string? raw) in op.Fields)
                {
                    if (!DecorationOps.SetFields.Contains(field.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        string? suggestion = Suggest(field, DecorationOps.SetFields);
                        log.Warn(opPath.Field("Fields").Field(field), $"Set cannot change '{field}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; it accepts {string.Join(", ", DecorationOps.SetFields)}.");
                    }

                    if (raw != null)
                    {
                        CheckTemplate(raw, opPath.Field("Fields").Field(field), log);
                    }
                }
            }
        }
    }
}
