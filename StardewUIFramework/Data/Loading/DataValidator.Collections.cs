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
    /// <summary>Collections, rich tooltips and forms (v1.5): row sources, list / grid / form members, templates, items and sprites.</summary>
    internal sealed partial class DataValidator
    {
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
                    CheckSource(source, path.Field(name), log);
                }
            }
        }

        /// <summary>Check a row source (kind, members, expressions, state key, named entry).</summary>
        private void CheckSource(SourceDefinition? source, DataPath path, DataMessageLog log)
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

            CheckSourceKind(source, kind, path, log);

            CheckExpression(source.Filter, path.Field("Filter"), log);
            CheckExpression(source.Sort, path.Field("Sort"), log);
        }

        /// <summary>Check the members of a row source of kind <paramref name="kind"/>.</summary>
        private void CheckSourceKind(SourceDefinition source, string kind, DataPath path, DataMessageLog log)
        {
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
        }

        /// <summary>Check the members of List, DataGrid, Repeat, Grid (column tracks) and Form elements, and their templates.</summary>
        private void CheckCollection(ElementDefinition def, string type, string owner, DataPath path, DataMessageLog log)
        {
            switch (type)
            {
                case ElementTypes.List:
                    CheckSource(def.Source, path.Field("Source"), log);
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
                    CheckSource(def.Source, path.Field("Source"), log);
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
                    CheckSource(def.Repeat, path.Field("Repeat"), log);
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

                if (field.Kind != null && field.Kind.Trim().ToLowerInvariant() is not ("checkbox" or "number" or "integer" or "text" or "dropdown"))
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
    }
}
