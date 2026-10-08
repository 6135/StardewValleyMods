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
    /// <summary>
    /// Checks and normalizes a (copied) menu definition before it is built. Every problem is a path-qualified message;
    /// only a bad key or an owner that is not loaded rejects the entry, anything else skips the offending value or
    /// element and the rest still builds. Normalization expands the shorthand forms into <see cref="ElementDefinition.Type"/>
    /// plus the main value, canonicalizes type names and generates missing ids as <c>&lt;parent&gt;.&lt;index&gt;</c>.
    /// </summary>
    internal sealed partial class DataValidator
    {
        private static readonly Dictionary<Type, PropertyInfo[]> ModelPropertyCache = new();
        private static readonly Dictionary<Type, string[]> ModelNameCache = new();

        private readonly Func<string, bool> isLoaded;
        private readonly SpriteRefs sprites;
        private readonly Func<string, OwnerDefinition?> owners;
        private readonly Func<string, DataCompositeDefinition?> dataComposites;

        /// <summary>The templates visible to the entry being checked (v1.7: its own, then its owner's).</summary>
        private Func<string, TemplateDefinition?> templates = _ => null;

        /// <summary>Above 0 while a template / composite body is checked (Outlet placeholders are expected there).</summary>
        private int bodyDepth;

        internal DataValidator(Func<string, bool> isLoaded, SpriteRefs sprites, Func<string, OwnerDefinition?>? owners = null, Func<string, DataCompositeDefinition?>? dataComposites = null)
        {
            this.isLoaded = isLoaded;
            this.sprites = sprites;
            this.owners = owners ?? (_ => null);
            this.dataComposites = dataComposites ?? (_ => null);
        }

        /// <summary>A template lookup: <paramref name="local"/> first, then the owner's <c>Templates</c>.</summary>
        private Func<string, TemplateDefinition?> TemplateLookup(Dictionary<string, TemplateDefinition>? local, string owner)
        {
            return name =>
            {
                string key = name.Trim();
                TemplateDefinition? found = local?.FirstOrDefault(p => string.Equals(p.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)).Value;
                return found ?? owners(owner)?.Templates?.FirstOrDefault(p => string.Equals(p.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)).Value;
            };
        }

        /// <summary>Split a <c>&lt;owner&gt;/&lt;id&gt;</c> key; false when it has no owner or no id.</summary>
        internal static bool TrySplitKey(string key, out string owner, out string id)
        {
            int slash = key.IndexOf('/');
            owner = slash > 0 ? key.Substring(0, slash).Trim() : string.Empty;
            id = slash > 0 ? key.Substring(slash + 1).Trim() : string.Empty;
            return owner.Length > 0 && id.Length > 0;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Menus
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Validate and normalize a menu entry; false when the entry must be rejected (bad key, owner not loaded).</summary>
        internal bool ValidateMenu(string key, MenuDefinition def, DataMessageLog log, out string owner, out string menuId)
        {
            DataPath path = DataPath.Entry(DataAssets.ShortName(DataAssets.Menus), key);
            if (!TrySplitKey(key, out owner, out menuId))
            {
                log.Error(path, "the key must be '<owner mod id>/<menu id>'; the entry is skipped.");
                return false;
            }

            if (!isLoaded(owner))
            {
                log.Error(path, $"the owner '{owner}' is not a loaded mod or content pack; the entry is skipped.");
                return false;
            }

            CheckUnknown(def.Unknown, typeof(MenuDefinition), path, log);
            if (def.Hotkey != null && !ValueParsers.Keybind.Parse(def.Hotkey, out _))
            {
                log.Warn(path.Field("Hotkey"), $"'{def.Hotkey}' is not {ValueParsers.Keybind.Description}; no hotkey is bound.");
            }

            CheckCondition(def.Condition, path.Field("Condition"), log);
            CheckActions(def.OnOpen, path.Field("OnOpen"), log);
            CheckActions(def.OnClose, path.Field("OnClose"), log);
            CheckActions(def.OnScroll, path.Field("OnScroll"), log);
            CheckActions(def.OnUpdate, path.Field("OnUpdate"), log);
            CheckKeys(def.Keys, path.Field("Keys"), log);
            CheckTemplates(def, path, log);
            if (def.StateLifetime != null && !Enum.TryParse(def.StateLifetime.Trim(), ignoreCase: true, out StateLifetime _))
            {
                log.Warn(path.Field("StateLifetime"), $"'{def.StateLifetime}' is not Session or Open; Session is used.");
            }

            CheckStateMembers(def.State, def.Computed, def.Watch, DataScope.ForMenu(owner, menuId, null), path, log);
            CheckSources(def.Sources, owner, path.Field("Sources"), log);

            templates = TemplateLookup(def.Templates, owner);
            sourceNames = def.Sources?.Keys;
            CheckTemplateDefinitions(def.Templates, owner, path.Field("Templates"), log);
            CheckExposures(def.Expose, def.Commands, path, log);

            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            CheckChildren(def.Children, menuId, owner, path.Field("Children"), types, log);

            CheckButtonRef(def.DefaultButton, types, path.Field("DefaultButton"), log);
            CheckButtonRef(def.CancelButton, types, path.Field("CancelButton"), log);
            return true;
        }

        private void CheckChildren(List<ElementDefinition>? children, string parentId, string owner, DataPath path, Dictionary<string, string> types, DataMessageLog log)
        {
            if (children == null)
            {
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                ElementDefinition? child = children[i];
                if (child == null)
                {
                    log.Warn(path.Index(i, null), "empty element; it is skipped.");
                    continue;
                }

                CheckElement(child, parentId, i, owner, path.Index(i, child.Id), types, log);
            }
        }

        private void CheckElement(ElementDefinition def, string parentId, int index, string owner, DataPath path, Dictionary<string, string> types, DataMessageLog log)
        {
            if (!ElementTypes.IsCustomTag(def.Type) && !IsTemplateInstance(def))
            {
                CheckUnknown(def.Unknown, typeof(ElementDefinition), path, log); // a custom tag's (or template instance's) extra fields are its arguments
            }

            string? type = NormalizeType(def, path, log);

            string id = AssignId(def, parentId, index, path, log);
            if (types.ContainsKey(id))
            {
                log.Warn(path.Field("Id"), $"id '{id}' is used more than once in this menu; lookups by id return the first one.");
            }
            else
            {
                types[id] = type ?? string.Empty;
            }

            if (type == null)
            {
                return;
            }

            CheckUnused(def, type, path, log);
            CheckCondition(def.Condition, path.Field("Condition"), log);
            CheckTemplates(def, path, log);
            CheckExpression(def.If, path.Field("If"), log);
            CheckExpression(def.Switch, path.Field("Switch"), log);
            CheckExpression(def.Validate, path.Field("Validate"), log);
            CheckBindKey(def.Bind, path.Field("Bind"), log);
            CheckKeys(def.Keys, path.Field("Keys"), log);
            if (type == ElementTypes.Composite && string.IsNullOrWhiteSpace(def.Composite))
            {
                log.Error(path.Field("Composite"), "a Composite needs the name of a composite defined in C# or the Composites asset (\"Composite\": \"ModId.Name\"); nothing is built.");
            }

            CheckCompositeInstance(def, type, path, log);
            CheckTemplateInstance(def, type, path, log);
            CheckElementBindings(def, path, log);
            if (type == ElementTypes.Switch && (def.Children == null || def.Children.Count == 0))
            {
                log.Warn(path.Field("Children"), "a Switch needs pages (children with a Case).");
            }

            CheckCollection(def, type, owner, path, log);
            CheckTooltip(def.RichTooltip, owner, path.Field("RichTooltip"), log);
            CheckSprite(def.Sprite, owner, path.Field("Sprite"), log);
            CheckSprite(def.Icon, owner, path.Field("Icon"), log);
            CheckSprite(def.Texture, owner, path.Field("Texture"), log);
            CheckStyle(def, type, owner, path, log);
            CheckTypeValues(def, type, path, log);
            foreach ((string name, List<ActionDefinition>? actions) in EventLists(def))
            {
                CheckActions(actions, path.Field(name), log);
            }

            if (ElementTypes.IsContainer(type))
            {
                CheckChildren(def.Children, id, owner, path.Field("Children"), types, log);
            }
        }

        /// <summary>Generate a missing id (<c>&lt;parent&gt;.&lt;index&gt;</c>) or trim the given one.</summary>
        private static string AssignId(ElementDefinition def, string parentId, int index, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(def.Id))
            {
                def.Id = parentId + "." + index;
                log.Info(path, $"no Id; using '{def.Id}' (Content Patcher TargetField cannot reach elements without an explicit Id).");
            }
            else
            {
                def.Id = def.Id.Trim();
            }

            return def.Id;
        }

        /// <summary>A data composite instance: its arguments, the events it publishes and its (ignored) ContentTarget.</summary>
        private void CheckCompositeInstance(ElementDefinition def, string type, DataPath path, DataMessageLog log)
        {
            if (type != ElementTypes.Composite || def.Composite == null || ExpressionValueResolver.HasTemplate(def.Composite) || dataComposites(def.Composite.Trim()) is not { } dataComposite)
            {
                return;
            }

            CheckArgs("composite", def.Composite.Trim(), dataComposite.Params, def.Args, path, log);
            if (def.On != null && dataComposite.Publish is { Count: > 0 } published)
            {
                foreach (string eventName in def.On.Keys)
                {
                    if (!published.Contains(eventName.Trim(), StringComparer.Ordinal))
                    {
                        string? suggestion = Suggest(eventName, published);
                        log.Warn(path.Field("On").Field(eventName), $"'{def.Composite}' does not publish '{eventName}' (its Publish lists {string.Join(", ", published)}){(suggestion != null ? $"; did you mean '{suggestion}'?" : string.Empty)}.");
                    }
                }
            }

            if (def.ContentTarget != null)
            {
                log.Warn(path.Field("ContentTarget"), $"'{def.Composite}' is a data composite: its children go into its Outlets (\"Outlet\": \"name\" on a child); ContentTarget is ignored.");
            }
        }

        /// <summary>A template instance (its template and arguments) and an Outlet placeholder outside a body.</summary>
        private void CheckTemplateInstance(ElementDefinition def, string type, DataPath path, DataMessageLog log)
        {
            if (type == ElementTypes.Template)
            {
                TemplateDefinition? template = def.Template == null ? null : templates(def.Template);
                if (template == null)
                {
                    log.Error(path.Field("Template"), $"no template '{def.Template}' in the menu's Templates or the owner's {DataAssets.ShortName(DataAssets.Owners)} entry; the instance is empty.");
                }
                else
                {
                    CheckArgs("template", def.Template!, template.Params, def.Args, path, log);
                }
            }

            if (type == ElementTypes.Outlet && bodyDepth == 0)
            {
                log.Warn(path, "an Outlet placeholder only receives children inside a template or data composite body; here it shows its own children.");
            }
        }

        /// <summary>The element's <c>On</c> handlers, <c>With</c> path and <c>Out</c> bindings.</summary>
        private static void CheckElementBindings(ElementDefinition def, DataPath path, DataMessageLog log)
        {
            if (def.On != null)
            {
                foreach ((string eventName, List<ActionDefinition>? actions) in def.On)
                {
                    CheckActions(actions, path.Field("On").Field(eventName), log);
                }
            }

            if (def.With != null)
            {
                CompiledExpression with = CompiledExpression.Compile(def.With.Trim());
                if (with.Root is not PathNode { StaticPath: not null })
                {
                    log.Error(path.Field("With"), $"'{def.With}' must be a plain path such as menu.settings{(with.Error != null ? $" ({with.Error})" : string.Empty)}.");
                }
            }

            if (def.Out != null)
            {
                foreach ((string key, string? target) in def.Out)
                {
                    if (!ElementTypes.OutKeys.Contains(key?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    {
                        string? suggestion = Suggest(key ?? string.Empty, ElementTypes.OutKeys);
                        log.Warn(path.Field("Out").Field(key ?? "?"), $"unknown output '{key}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; it is ignored.");
                    }

                    CheckStateKey(target, path.Field("Out").Field(key ?? "?"), log);
                }
            }
        }

        /// <summary>The element's inline <c>Style</c>: unknown members, the box texture and members its type does not use.</summary>
        private void CheckStyle(ElementDefinition def, string type, string owner, DataPath path, DataMessageLog log)
        {
            if (def.Style == null)
            {
                return;
            }

            CheckUnknown(def.Style.Unknown, typeof(StyleDefinition), path.Field("Style"), log);
            CheckSprite(def.Style.BoxTexture, owner, path.Field("Style").Field("BoxTexture"), log);
            foreach (PropertyInfo property in ModelProperties(typeof(StyleDefinition)))
            {
                if (property.GetValue(def.Style) != null && !ElementTypes.UsesStyle(type, property.Name))
                {
                    log.Warn(path.Field("Style").Field(property.Name), $"a {type} does not use Style.{property.Name}; it is ignored.");
                }
            }
        }

        /// <summary>The values of item images, images and dropdowns (item, source rectangle, choices and labels).</summary>
        private void CheckTypeValues(ElementDefinition def, string type, DataPath path, DataMessageLog log)
        {
            if (type == ElementTypes.ItemImage)
            {
                CheckItem(def.Item, path.Field("Item"), log, required: true);
            }

            if (type == ElementTypes.Image && def.Source != null && def.Source.Shorthand == null)
            {
                log.Warn(path.Field("Source"), "an Image's Source is a rectangle 'x,y,width,height'; it is ignored.");
            }

            if (type == ElementTypes.Dropdown)
            {
                if (def.ChoicesSource != null)
                {
                    CheckSource(def.ChoicesSource, path.Field("ChoicesSource"), log);
                    CheckExpression(def.ChoiceValue, path.Field("ChoiceValue"), log);
                    CheckExpression(def.ChoiceLabel, path.Field("ChoiceLabel"), log);
                    if (def.Choices != null)
                    {
                        log.Warn(path.Field("Choices"), "Choices are ignored when a ChoicesSource is set.");
                    }
                }
                else if (def.Choices == null || def.Choices.Count == 0)
                {
                    log.Warn(path.Field("Choices"), "a Dropdown needs Choices (or a ChoicesSource).");
                }
            }

            if (def.Labels != null && def.Choices != null && def.Labels.Count != def.Choices.Count)
            {
                log.Warn(path.Field("Labels"), $"{def.Labels.Count} label(s) for {def.Choices.Count} choice(s); missing labels show the value.");
            }
        }

        /// <summary>The element types a shorthand member stands for, in the order they are checked.</summary>
        private static readonly (Func<ElementDefinition, object?> Member, string Type)[] Shorthands =
        {
            (d => d.Switch, ElementTypes.Switch),
            (d => d.Repeat, ElementTypes.Repeat),
            (d => d.Button, ElementTypes.Button),
            (d => d.Checkbox, ElementTypes.Checkbox),
            (d => d.Image, ElementTypes.Image),
            (d => d.Label, ElementTypes.Label),
            (d => d.Children, ElementTypes.Stack),
            (d => d.Outlet, ElementTypes.Outlet) // { "Outlet": "header" }: a placeholder of a template / composite body
        };

        /// <summary>Expand the shorthand forms and canonicalize the type; null (logged) when the element has no usable type.</summary>
        private string? NormalizeType(ElementDefinition def, DataPath path, DataMessageLog log)
        {
            string? type = ResolveType(def, path, log);
            if (type == null)
            {
                return null;
            }

            def.Type = type;
            switch (type)
            {
                case ElementTypes.Label:
                    def.Text ??= def.Label;
                    def.Label = null;
                    break;
                case ElementTypes.Button:
                    def.Text ??= def.Button;
                    def.Button = null;
                    break;
                case ElementTypes.Checkbox:
                    def.Label ??= def.Checkbox ?? def.Text;
                    def.Checkbox = null;
                    def.Text = null;
                    break;
                case ElementTypes.Image:
                    def.Sprite ??= def.Image;
                    def.Image = null;
                    break;
            }

            return type;
        }

        /// <summary>The element's canonical type: a template instance, a custom tag, its Type or a shorthand; null (logged) when it has none.</summary>
        private string? ResolveType(ElementDefinition def, DataPath path, DataMessageLog log)
        {
            if (IsTemplateInstance(def))
            {
                // a template instance ("Type": "<template>" or "Template": "<template>"): its extra fields are the arguments
                def.Template = (def.Template ?? def.Type)!.Trim();
                ExpandTag(def);
                def.Type = ElementTypes.Template;
                return ElementTypes.Template;
            }

            if (ElementTypes.IsCustomTag(def.Type))
            {
                def.Composite ??= def.Type!.Trim();
                ExpandTag(def);
                def.Type = ElementTypes.Composite;
                return ElementTypes.Composite;
            }

            if (def.Type != null)
            {
                string? type = ElementTypes.Canonical(def.Type);
                if (type == null)
                {
                    string? suggestion = Suggest(def.Type, ElementTypes.All);
                    log.Error(path.Field("Type"), $"unknown element type '{def.Type}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; the element is skipped.");
                    def.Type = null;
                }

                return type;
            }

            foreach ((Func<ElementDefinition, object?> member, string shorthandType) in Shorthands)
            {
                if (member(def) != null)
                {
                    return shorthandType;
                }
            }

            log.Error(path, "the element has no Type (or shorthand such as \"Label\": \"text\"); it is skipped.");
            return null;
        }

        /// <summary>True when <paramref name="def"/> is a template instance (v1.7): a <c>Template</c>, or a Type naming a template.</summary>
        private bool IsTemplateInstance(ElementDefinition def)
        {
            if (def.Template != null)
            {
                return true;
            }

            return def.Type != null && ElementTypes.Canonical(def.Type) == null && templates(def.Type.Trim()) != null;
        }

        /// <summary>
        /// A custom tag (<c>{ "Type": "6135.Example.Gauge", "Value": "menu.volume" }</c>) or template instance as the
        /// element it stands for: every field that is not common to all elements (nor Children / ContentTarget / On)
        /// moves into <c>Args</c> (explicit Args win).
        /// </summary>
        private static void ExpandTag(ElementDefinition def)
        {
            var args = new Dictionary<string, JToken?>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyInfo property in ModelProperties(typeof(ElementDefinition)))
            {
                if (property.Name is nameof(ElementDefinition.Type) or nameof(ElementDefinition.Children) or nameof(ElementDefinition.Composite)
                    or nameof(ElementDefinition.Args) or nameof(ElementDefinition.ContentTarget) or nameof(ElementDefinition.On) or nameof(ElementDefinition.Template)
                    || ElementTypes.Common.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                object? value = property.GetValue(def);
                if (value != null)
                {
                    args[property.Name] = JToken.FromObject(value);
                    property.SetValue(def, null);
                }
            }

            if (def.Unknown != null)
            {
                foreach ((string name, JToken value) in def.Unknown)
                {
                    if (!name.StartsWith('$'))
                    {
                        args[name] = value;
                    }
                }

                def.Unknown = null;
            }

            if (def.Args != null)
            {
                foreach ((string name, JToken? value) in def.Args)
                {
                    args[name] = value;
                }
            }

            def.Args = args.Count > 0 ? args : null;
        }

        /// <summary>Warn about members the element's type does not read.</summary>
        private static void CheckUnused(ElementDefinition def, string type, DataPath path, DataMessageLog log)
        {
            foreach (PropertyInfo property in ModelProperties(typeof(ElementDefinition)))
            {
                if (property.GetValue(def) != null && !ElementTypes.Uses(type, property.Name))
                {
                    log.Warn(path.Field(property.Name), $"{property.Name} is not used by a {type}; it is ignored.");
                }
            }
        }

        /// <summary>Every action list member of an element.</summary>
        internal static IEnumerable<(string Name, List<ActionDefinition>? Actions)> EventLists(ElementDefinition def)
        {
            yield return (nameof(def.OnClick), def.OnClick);
            yield return (nameof(def.OnRightClick), def.OnRightClick);
            yield return (nameof(def.OnHover), def.OnHover);
            yield return (nameof(def.OnHoverEnd), def.OnHoverEnd);
            yield return (nameof(def.OnFocus), def.OnFocus);
            yield return (nameof(def.OnBlur), def.OnBlur);
            yield return (nameof(def.OnValueChanged), def.OnValueChanged);
            yield return (nameof(def.OnSubmit), def.OnSubmit);
            yield return (nameof(def.OnLink), def.OnLink);
            yield return (nameof(def.OnScroll), def.OnScroll);
            yield return (nameof(def.OnInvalid), def.OnInvalid);
            yield return (nameof(def.OnRowClick), def.OnRowClick);
            yield return (nameof(def.OnRowActivated), def.OnRowActivated);
            yield return (nameof(def.OnColumnResized), def.OnColumnResized);
            yield return (nameof(def.OnSaved), def.OnSaved);
            yield return (nameof(def.OnCancelled), def.OnCancelled);
            yield return (nameof(def.OnChanged), def.OnChanged);
        }
    }
}
