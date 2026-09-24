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
    internal sealed class DataValidator
    {
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
            sourceNames = def.Sources?.Keys;
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

            if (string.IsNullOrWhiteSpace(def.Id))
            {
                def.Id = parentId + "." + index;
                log.Info(path, $"no Id; using '{def.Id}' (Content Patcher TargetField cannot reach elements without an explicit Id).");
            }
            else
            {
                def.Id = def.Id.Trim();
            }

            if (types.ContainsKey(def.Id))
            {
                log.Warn(path.Field("Id"), $"id '{def.Id}' is used more than once in this menu; lookups by id return the first one.");
            }
            else
            {
                types[def.Id] = type ?? string.Empty;
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

            if (type == ElementTypes.Composite && def.Composite != null && !ExpressionValueResolver.HasTemplate(def.Composite) && dataComposites(def.Composite.Trim()) is { } dataComposite)
            {
                CheckArgs("composite", def.Composite.Trim(), dataComposite.Params, def.Args, path, log);
                if (def.ContentTarget != null)
                {
                    log.Warn(path.Field("ContentTarget"), $"'{def.Composite}' is a data composite: its children go into its Outlets (\"Outlet\": \"name\" on a child); ContentTarget is ignored.");
                }
            }

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

            if (type == ElementTypes.Switch && (def.Children == null || def.Children.Count == 0))
            {
                log.Warn(path.Field("Children"), "a Switch needs pages (children with a Case).");
            }

            CheckCollection(def, type, owner, path, log);
            CheckTooltip(def.RichTooltip, owner, path.Field("RichTooltip"), log);
            CheckSprite(def.Sprite, owner, path.Field("Sprite"), log);
            CheckSprite(def.Icon, owner, path.Field("Icon"), log);
            CheckSprite(def.Texture, owner, path.Field("Texture"), log);
            if (def.Style != null)
            {
                CheckUnknown(def.Style.Unknown, typeof(StyleDefinition), path.Field("Style"), log);
                CheckSprite(def.Style.BoxTexture, owner, path.Field("Style").Field("BoxTexture"), log);
            }

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
                    CheckSource(def.ChoicesSource, owner, path.Field("ChoicesSource"), log);
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

            foreach ((string name, List<ActionDefinition>? actions) in EventLists(def))
            {
                CheckActions(actions, path.Field(name), log);
            }

            if (ElementTypes.IsContainer(type))
            {
                CheckChildren(def.Children, def.Id, owner, path.Field("Children"), types, log);
            }
        }

        /// <summary>Expand the shorthand forms and canonicalize the type; null (logged) when the element has no usable type.</summary>
        private string? NormalizeType(ElementDefinition def, DataPath path, DataMessageLog log)
        {
            string? type;
            if (IsTemplateInstance(def))
            {
                // a template instance ("Type": "<template>" or "Template": "<template>"): its extra fields are the arguments
                def.Template = (def.Template ?? def.Type)!.Trim();
                ExpandTag(def);
                def.Type = ElementTypes.Template;
                type = ElementTypes.Template;
            }
            else if (ElementTypes.IsCustomTag(def.Type))
            {
                def.Composite ??= def.Type!.Trim();
                ExpandTag(def);
                def.Type = ElementTypes.Composite;
                type = ElementTypes.Composite;
            }
            else if (def.Type != null)
            {
                type = ElementTypes.Canonical(def.Type);
                if (type == null)
                {
                    string? suggestion = Suggest(def.Type, ElementTypes.All);
                    log.Error(path.Field("Type"), $"unknown element type '{def.Type}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; the element is skipped.");
                    def.Type = null;
                    return null;
                }
            }
            else if (def.Switch != null)
            {
                type = ElementTypes.Switch;
            }
            else if (def.Repeat != null)
            {
                type = ElementTypes.Repeat;
            }
            else if (def.Button != null)
            {
                type = ElementTypes.Button;
            }
            else if (def.Checkbox != null)
            {
                type = ElementTypes.Checkbox;
            }
            else if (def.Image != null)
            {
                type = ElementTypes.Image;
            }
            else if (def.Label != null)
            {
                type = ElementTypes.Label;
            }
            else if (def.Children != null)
            {
                type = ElementTypes.Stack;
            }
            else if (def.Outlet != null)
            {
                type = ElementTypes.Outlet; // { "Outlet": "header" }: a placeholder of a template / composite body
            }
            else
            {
                log.Error(path, "the element has no Type (or shorthand such as \"Label\": \"text\"); it is skipped.");
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

        // ---------------------------------------------------------------------------------------------------------
        //  Collections, rich tooltips and forms (v1.5)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The names of the entry's <c>Sources</c> (while its tree is checked).</summary>
        private IEnumerable<string>? sourceNames;

        /// <summary>Check a UI's named <c>Sources</c>.</summary>
        private void CheckSources(Dictionary<string, SourceDefinition>? sources, string owner, DataPath path, DataMessageLog log)
        {
            if (sources == null)
            {
                return;
            }

            sourceNames = sources.Keys;
            foreach ((string name, SourceDefinition? source) in sources)
            {
                if (source != null)
                {
                    CheckSource(source, owner, path.Field(name), log);
                }
            }
        }

        /// <summary>Check a row source (kind, members, expressions, state key, named entry).</summary>
        private void CheckSource(SourceDefinition? source, string owner, DataPath path, DataMessageLog log)
        {
            if (source == null)
            {
                return;
            }

            CheckUnknown(source.Unknown, typeof(SourceDefinition), path, log);
            if (source.Shorthand is { } shorthand)
            {
                // a Sources entry name or a state key
                bool named = sourceNames?.Contains(shorthand.Trim(), StringComparer.OrdinalIgnoreCase) ?? false;
                if (!named)
                {
                    CheckStateKey(shorthand, path, log);
                }

                return;
            }

            string? kind = SourceKinds.Canonical(source.Kind);
            if (kind == null)
            {
                string? suggestion = source.Type != null ? Suggest(source.Type, SourceKinds.All) : null;
                log.Error(path.Field("Type"), source.Type != null
                    ? $"unknown source Type '{source.Type}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}."
                    : "the source says nowhere the rows come from (Rows, From / To, Query, State, Asset, Hook or Name).");
                return;
            }

            switch (kind)
            {
                case SourceKinds.Range:
                    if (source.To == null)
                    {
                        log.Warn(path.Field("To"), "a Range needs To (and usually From).");
                    }

                    CheckExpression(source.From, path.Field("From"), log);
                    CheckExpression(source.To, path.Field("To"), log);
                    CheckExpression(source.Step, path.Field("Step"), log);
                    break;
                case SourceKinds.ItemQuery:
                    if (string.IsNullOrWhiteSpace(source.Query))
                    {
                        log.Warn(path.Field("Query"), "an ItemQuery needs a Query (e.g. ALL_ITEMS (O)).");
                    }
                    else
                    {
                        CheckTemplate(source.Query, path.Field("Query"), log);
                    }

                    CheckCondition(source.PerItemCondition, path.Field("PerItemCondition"), log);
                    break;
                case SourceKinds.State:
                    CheckStateKey(source.State, path.Field("State"), log);
                    break;
                case SourceKinds.Value:
                    CheckExpression(source.Value, path.Field("Value"), log);
                    break;
                case SourceKinds.Named:
                    if (source.Name == null || !(sourceNames?.Contains(source.Name.Trim(), StringComparer.OrdinalIgnoreCase) ?? false))
                    {
                        string? suggestion = sourceNames != null ? Suggest(source.Name ?? string.Empty, sourceNames) : null;
                        log.Error(path.Field("Name"), $"no entry '{source.Name}' in Sources{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    }

                    break;
                case SourceKinds.Hook:
                    if (string.IsNullOrWhiteSpace(source.Hook))
                    {
                        log.Warn(path.Field("Hook"), "a Hook source needs the name of a C# source (DefineDataSource / ExposeRows): \"hook:ModId/name\".");
                    }

                    break;
                case SourceKinds.Asset:
                    if (string.IsNullOrWhiteSpace(source.Asset))
                    {
                        log.Warn(path.Field("Asset"), "an Asset source needs an Asset name (a Dictionary<string, string> asset).");
                    }

                    break;
            }

            CheckExpression(source.Filter, path.Field("Filter"), log);
            CheckExpression(source.Sort, path.Field("Sort"), log);
        }

        /// <summary>Check the members of List, DataGrid, Repeat, Grid (column tracks) and Form elements, and their templates.</summary>
        private void CheckCollection(ElementDefinition def, string type, string owner, DataPath path, DataMessageLog log)
        {
            switch (type)
            {
                case ElementTypes.List:
                    CheckSource(def.Source, owner, path.Field("Source"), log);
                    if (def.Source == null)
                    {
                        log.Warn(path.Field("Source"), "a List needs a Source; it shows no rows.");
                    }

                    if (def.RowTemplate == null || def.RowTemplate.Count == 0)
                    {
                        log.Warn(path.Field("RowTemplate"), "a List needs a RowTemplate (the elements of one row); its rows are empty.");
                    }

                    CheckTemplateTree(def.RowTemplate, def.Id + ".row", owner, path.Field("RowTemplate"), log);
                    CheckStateKey(def.BindSelected, path.Field("BindSelected"), log);
                    break;

                case ElementTypes.DataGrid:
                    CheckSource(def.Source, owner, path.Field("Source"), log);
                    if (def.Source == null)
                    {
                        log.Warn(path.Field("Source"), "a DataGrid needs a Source; it shows no rows.");
                    }

                    CheckGridColumns(def, owner, path, log);
                    CheckExpression(def.Filter, path.Field("Filter"), log);
                    CheckTooltip(def.RowTooltip, owner, path.Field("RowTooltip"), log);
                    CheckStateKey(def.BindSelected, path.Field("BindSelected"), log);
                    CheckStateKey(def.BindSelection, path.Field("BindSelection"), log);
                    break;

                case ElementTypes.Repeat:
                    CheckSource(def.Repeat, owner, path.Field("Repeat"), log);
                    if (def.Children == null || def.Children.Count == 0)
                    {
                        log.Warn(path.Field("Children"), "a Repeat needs Children (the elements copied for each row).");
                    }

                    CheckTemplateTree(def.Children, def.Id!, owner, path.Field("Children"), log);
                    break;

                case ElementTypes.Grid:
                    if (def.Columns != null)
                    {
                        for (int i = 0; i < def.Columns.Count; i++)
                        {
                            ColumnDefinition? column = def.Columns[i];
                            if (column != null && (column.Id != null || column.Header != null || column.Text != null || column.Cell != null))
                            {
                                log.Warn(path.Field("Columns").Index(i, column.Id), "a Grid's columns only have a Width (a track); the other members are for a DataGrid.");
                            }
                        }
                    }

                    break;

                case ElementTypes.Form:
                    if (def.Model != null)
                    {
                        if (def.Fields != null)
                        {
                            log.Warn(path.Field("Fields"), "Fields are ignored when the Form has a Model (its members are the fields).");
                        }
                    }
                    else
                    {
                        CheckFormFields(def.Fields, path.Field("Fields"), log);
                    }

                    break;
            }

            if (def.As != null && !IsIdentifier(def.As.Trim()))
            {
                log.Error(path.Field("As"), $"'{def.As}' is not a variable name (letters, digits and _ , not starting with a digit).");
            }
        }

        private void CheckGridColumns(ElementDefinition def, string owner, DataPath path, DataMessageLog log)
        {
            if (def.Columns == null || def.Columns.Count == 0)
            {
                log.Warn(path.Field("Columns"), "a DataGrid needs Columns ([{ \"Id\": \"name\", \"Header\": \"Name\", \"Text\": \"${row.name}\" }, ...]).");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < def.Columns.Count; i++)
            {
                ColumnDefinition? column = def.Columns[i];
                DataPath columnPath = path.Field("Columns").Index(i, column?.Id);
                if (column == null)
                {
                    continue;
                }

                CheckUnknown(column.Unknown, typeof(ColumnDefinition), columnPath, log);
                if (string.IsNullOrWhiteSpace(column.Id))
                {
                    log.Error(columnPath.Field("Id"), "a DataGrid column needs an Id; the column is skipped.");
                    continue;
                }

                if (!ids.Add(column.Id.Trim()))
                {
                    log.Warn(columnPath.Field("Id"), $"column id '{column.Id}' is used more than once; the last one wins.");
                }

                CheckTemplates(column, columnPath, log);
                CheckExpression(column.SortNumber, columnPath.Field("SortNumber"), log);
                CheckExpression(column.SortKey, columnPath.Field("SortKey"), log);
                CheckTemplateTree(column.Cell, def.Id + ".cell", owner, columnPath.Field("Cell"), log);
            }

            if (!string.IsNullOrWhiteSpace(def.Sort))
            {
                string sortColumn = def.Sort.Trim().Split(' ')[0];
                if (!ids.Contains(sortColumn))
                {
                    string? suggestion = Suggest(sortColumn, ids);
                    log.Warn(path.Field("Sort"), $"no column has the id '{sortColumn}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                }
            }
        }

        /// <summary>Check a row / cell template: its elements are checked with their own id table (they are built under each row's id).</summary>
        private void CheckTemplateTree(List<ElementDefinition>? template, string parentId, string owner, DataPath path, DataMessageLog log)
        {
            if (template == null)
            {
                return;
            }

            CheckChildren(template, parentId, owner, path, new Dictionary<string, string>(StringComparer.Ordinal), log);
        }

        /// <summary>Check a rich tooltip definition (block types, expressions, images, items).</summary>
        private void CheckTooltip(TooltipDefinition? tooltip, string owner, DataPath path, DataMessageLog log)
        {
            if (tooltip == null)
            {
                return;
            }

            CheckUnknown(tooltip.Unknown, typeof(TooltipDefinition), path, log);
            bool named = false;
            if (!string.IsNullOrWhiteSpace(tooltip.From))
            {
                named = owners(owner)?.Tooltips?.ContainsKey(tooltip.From.Trim()) == true;
                if (!named)
                {
                    log.Error(path.Field("From"), $"'{owner}' has no tooltip '{tooltip.From.Trim()}' in {DataAssets.Owners} (Tooltips).");
                }
            }

            if (tooltip.Blocks == null || tooltip.Blocks.Count == 0)
            {
                if (!named)
                {
                    log.Warn(path.Field("Blocks"), "the rich tooltip has no blocks.");
                }

                return;
            }

            for (int i = 0; i < tooltip.Blocks.Count; i++)
            {
                TooltipBlockDefinition? block = tooltip.Blocks[i];
                DataPath blockPath = path.Field("Blocks").Index(i, null);
                if (block == null)
                {
                    continue;
                }

                CheckUnknown(block.Unknown, typeof(TooltipBlockDefinition), blockPath, log);
                string? kind = TooltipBlockKinds.Canonical(block.Type ?? (block.Text != null ? "Line" : null));
                if (kind == null)
                {
                    string? suggestion = block.Type != null ? Suggest(block.Type, TooltipBlockKinds.All) : null;
                    log.Error(blockPath.Field("Type"), $"'{block.Type}' is not a block type (Title, Line, Icon, Item, Divider, Money){(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    continue;
                }

                CheckTemplates(block, blockPath, log);
                CheckExpression(block.When, blockPath.Field("When"), log);
                switch (kind)
                {
                    case "Icon":
                        if (string.IsNullOrWhiteSpace(block.Sprite))
                        {
                            log.Error(blockPath.Field("Sprite"), "an Icon block needs a Sprite.");
                        }
                        else
                        {
                            CheckSprite(block.Sprite, owner, blockPath.Field("Sprite"), log);
                        }

                        break;
                    case "Item":
                        CheckItem(block.Item, blockPath.Field("Item"), log, required: true);
                        break;
                    case "Money":
                        if (block.Amount == null)
                        {
                            log.Warn(blockPath.Field("Amount"), "a Money block needs an Amount.");
                        }

                        break;
                }
            }
        }

        /// <summary>Check an item value: a known id, an item query (checked when shown) or an expression.</summary>
        private static void CheckItem(string? item, DataPath path, DataMessageLog log, bool required)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                if (required)
                {
                    log.Warn(path, "an Item is needed (a qualified item id like (O)24, an item query or an expression such as ${row.item}); nothing is drawn.");
                }

                return;
            }

            string text = item.Trim();
            if (ExpressionValueResolver.HasTemplate(text) || text.Contains(' ', StringComparison.Ordinal))
            {
                return; // an expression or an item query: resolved when shown
            }

            if (ItemRegistry.GetData(text) == null)
            {
                log.Warn(path, $"no item matches '{item}'; the error item is shown.");
            }
        }

        /// <summary>Check a data form's fields.</summary>
        private static void CheckFormFields(List<FormFieldDefinition>? fields, DataPath path, DataMessageLog log)
        {
            if (fields == null || fields.Count == 0)
            {
                log.Warn(path, "a Form needs Fields.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Count; i++)
            {
                FormFieldDefinition? field = fields[i];
                DataPath fieldPath = path.Index(i, field?.Id);
                if (field == null)
                {
                    continue;
                }

                CheckUnknown(field.Unknown, typeof(FormFieldDefinition), fieldPath, log);
                if (field.Id == null && field.Bind == null)
                {
                    log.Warn(fieldPath, "the field has no Id or Bind; it is named field<index> and bound to menu.field<index>.");
                }
                else if (field.Id != null && !ids.Add(field.Id.Trim()))
                {
                    log.Warn(fieldPath.Field("Id"), $"field id '{field.Id}' is used more than once in this form.");
                }
                else
                {
                    // a usable id
                }

                if (field.Kind != null && field.Kind.Trim().ToLowerInvariant() is not ("checkbox" or "bool" or "number" or "integer" or "text" or "dropdown"))
                {
                    log.Error(fieldPath.Field("Kind"), $"'{field.Kind}' is not Checkbox, Number, Integer, Text or Dropdown; the field is skipped.");
                }

                if (field.Kind != null && field.Kind.Trim().Equals("Dropdown", StringComparison.OrdinalIgnoreCase) && (field.Choices == null || field.Choices.Count == 0))
                {
                    log.Warn(fieldPath.Field("Choices"), "a Dropdown field needs Choices.");
                }

                CheckTemplates(field, fieldPath, log);
                CheckExpression(field.Validate, fieldPath.Field("Validate"), log);
                CheckStateKey(field.Bind, fieldPath.Field("Bind"), log);
            }
        }

        private static bool IsIdentifier(string text)
        {
            return text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private static void CheckButtonRef(string? id, Dictionary<string, string> types, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (!types.TryGetValue(id.Trim(), out string? type))
            {
                log.Warn(path, $"no element has the id '{id}'.");
            }
            else if (type != ElementTypes.Button)
            {
                log.Warn(path, $"'{id}' is a {type}, not a Button.");
            }
            else
            {
                // a button
            }
        }

        private void CheckSprite(string? reference, string owner, DataPath path, DataMessageLog log)
        {
            // a live ${...} reference is checked as an expression (CheckTemplates) and resolved when it is read
            if (!string.IsNullOrWhiteSpace(reference) && !ExpressionValueResolver.HasTemplate(reference) && !sprites.Check(reference, owner, out string error))
            {
                log.Warn(path, error);
            }
        }

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
        //  Templates, composites and contributions (v1.7)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The parameter types a template / composite may declare.</summary>
        private static readonly string[] ParamTypes = { "string", "text", "number", "int", "integer", "double", "bool", "boolean", "any" };

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
            foreach (ElementDefinition? child in children)
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
                    "number" or "int" or "integer" or "double" => ValueParsers.Number.Parse(text, out _),
                    "bool" or "boolean" => ValueParsers.Bool.Parse(text, out _),
                    _ => true
                };
            }

            return kind switch
            {
                "number" or "int" or "integer" or "double" => value.Type is JTokenType.Integer or JTokenType.Float,
                "bool" or "boolean" => value.Type == JTokenType.Boolean,
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

                CheckUnknown(op.Unknown, typeof(DecorationOp), opPath, log);
                string? kind = DecorationOps.Canonical(op.Op);
                if (kind == null)
                {
                    string? suggestion = Suggest(op.Op ?? string.Empty, DecorationOps.All);
                    log.Error(opPath.Field("Op"), $"'{op.Op}' is not an operation ({string.Join(", ", DecorationOps.All)}){(suggestion != null ? $"; did you mean '{suggestion}'?" : string.Empty)}. It is skipped.");
                    continue;
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

                    CheckChildren(op.Children, name + "." + i, contributor, opPath.Field("Children"), new Dictionary<string, string>(StringComparer.Ordinal), log);
                }
                else if (op.Children != null)
                {
                    log.Warn(opPath.Field("Children"), $"{kind} does not add Children; they are ignored.");
                }

                if (kind == DecorationOps.Set)
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

                if (kind == DecorationOps.Move && op.Before == null && op.After == null && op.Into == null)
                {
                    log.Error(opPath, "Move needs Before, After or Into (an element id).");
                }
            }
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

        // ---------------------------------------------------------------------------------------------------------
        //  Typos
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The members of a definition model a JSON file may set (not the extension data bag).</summary>
        internal static IEnumerable<PropertyInfo> ModelProperties(Type model)
        {
            return model.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.Name != "Unknown" && p.CanWrite);
        }

        private static void CheckUnknown(IDictionary<string, JToken>? unknown, Type model, DataPath path, DataMessageLog log)
        {
            if (unknown == null || unknown.Count == 0)
            {
                return;
            }

            string[] known = ModelProperties(model).Select(p => p.Name).ToArray();
            foreach (string name in unknown.Keys)
            {
                if (name.StartsWith('$'))
                {
                    continue; // "$schema" and other editor metadata
                }

                string? suggestion = Suggest(name, known);
                log.Warn(path.Field(name), $"unknown field '{name}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; it is ignored.");
            }
        }

        /// <summary>The closest candidate by edit distance (case-insensitive), or null when none is close.</summary>
        internal static string? Suggest(string text, IEnumerable<string> candidates)
        {
            string lower = text.ToLowerInvariant();
            string? best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in candidates)
            {
                int distance = Distance(lower, candidate.ToLowerInvariant());
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best != null && bestDistance <= Math.Max(2, text.Length / 3) ? best : null;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[b.Length];
        }
    }
}
