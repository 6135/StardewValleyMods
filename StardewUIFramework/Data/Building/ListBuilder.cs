using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>A collection element built from data (<c>List</c>, <c>DataGrid</c>, <c>Repeat</c>), for <c>_Rebuild</c>.</summary>
    internal interface IDataCollection
    {
        /// <summary>The element.</summary>
        UIElement Element { get; }

        /// <summary>Re-read the source(s) and rebuild every row (<c>_Rebuild</c>).</summary>
        void Rebuild();
    }

    /// <summary>
    /// <c>List</c> (a virtualized <see cref="ListView"/> whose rows are built from <c>RowTemplate</c>) and dropdowns with a
    /// <c>ChoicesSource</c>. Rows are built into the list's row containers with ids prefixed by the container's id
    /// (<c>list.row3.name</c>), never by the item index, so fast scrolling never collides ids; each row container has its
    /// own refresh group, rebuilt whenever the container shows another item.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        private const int DefaultRowHeight = 48;
        private const int DefaultVisibleRows = 6;

        // ---------------------------------------------------------------------------------------------------------
        //  Row templates
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The row / cell containers of a virtualized collection and their refresh groups: a template is built into a
        /// container under the container's id; building again (the container shows another item) drops the previous
        /// row's refreshers.
        /// </summary>
        private sealed class TemplateRows
        {
            private readonly DataBuilder builder;
            private readonly BuildContext ctx;
            private readonly List<ElementDefinition>? template;
            private readonly DataPath path;
            private readonly Dictionary<UIElement, RefresherGroup> groups = new();

            internal TemplateRows(DataBuilder builder, BuildContext ctx, List<ElementDefinition>? template, DataPath path)
            {
                this.builder = builder;
                this.ctx = ctx;
                this.template = template;
                this.path = path;
            }

            /// <summary>Build the template into <paramref name="container"/> for a row scope.</summary>
            internal void Build(IUIContainer container, DataScope rowScope)
            {
                var element = (UIElement)container;
                if (!groups.TryGetValue(element, out RefresherGroup? group))
                {
                    groups[element] = group = new RefresherGroup(ctx.Runtime.Owner, element.Id);
                }

                group.Clear();
                if (template == null)
                {
                    return;
                }

                builder.BuildChildren(ctx.ForTemplate(group, element.Id), container, template, rowScope, path);
            }

            /// <summary>Refresh the live values of the rows that are still attached.</summary>
            internal void Refresh(bool opening)
            {
                foreach ((UIElement container, RefresherGroup group) in groups.ToArray())
                {
                    if (container.OwnerMenu == null)
                    {
                        groups.Remove(container); // a row container removed with VisibleRows
                        continue;
                    }

                    group.Refresh(opening);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  List
        // ---------------------------------------------------------------------------------------------------------

        private IUIElement CreateList(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            PropertyApplier a = ctx.Applier;
            int rowHeight = Math.Max(1, a.Initial(def.RowHeight, ValueParsers.Int, DefaultRowHeight, scope, path.Field("RowHeight")));
            int visibleRows = Math.Max(1, a.Initial(def.VisibleRows, ValueParsers.Int, DefaultVisibleRows, scope, path.Field("VisibleRows")));
            SourceBinding? source = CreateSource(ctx, def.Source, scope, def.As, path.Field("Source"), "a List");
            source?.Update(opening: true);
            var rows = new TemplateRows(this, ctx, def.RowTemplate, path.Field("RowTemplate"));
            IUIList list = ctx.Api.AddList(parent, ctx.IdOf(def), rowHeight, visibleRows, () => source?.Count ?? 0,
                (index, container) => rows.Build(container, source?.ScopeFor(index) ?? RowScope.For(scope, DataValue.Null, index, def.As)));

            var element = (UIElement)list;
            SelectionBinding? selected = SelectionBinding.Create(def.BindSelected, scope, path.Field("BindSelected"), ctx.Log, store,
                () => list.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                text => list.SelectedIndex = ValueParsers.TryParseInt(text, out int index) ? index : -1);
            var collection = new ListCollection(list, source, rows, selected);
            ctx.Runtime.Collections[element.Id] = collection;
            a.AddRefresher(collection);
            return list;
        }

        /// <summary>The source of a collection, with a warning when it has none.</summary>
        private static SourceBinding? CreateSource(BuildContext ctx, SourceDefinition? def, DataScope scope, string? alias, DataPath path, string what)
        {
            if (def == null)
            {
                ctx.Log.Warn(path, $"{what} needs a Source (e.g. \"menu.items\", \"range:1..10\" or {{ \"Rows\": [...] }}); it shows no rows.");
                return null;
            }

            return SourceBinding.Create(def, scope, alias, ctx.Runtime, path, ctx.Log);
        }

        /// <summary>Keeps a list in sync: source changes refresh it, the rows' live values refresh, the selection binding syncs.</summary>
        private sealed class ListCollection : IDataRefresher, IDataCollection
        {
            private readonly IUIList list;
            private readonly SourceBinding? source;
            private readonly TemplateRows rows;
            private readonly SelectionBinding? selected;

            internal ListCollection(IUIList list, SourceBinding? source, TemplateRows rows, SelectionBinding? selected)
            {
                this.list = list;
                this.source = source;
                this.rows = rows;
                this.selected = selected;
            }

            public UIElement Element => (UIElement)list;

            internal SourceBinding? Source => source;

            public void Refresh(bool opening)
            {
                if (source != null && source.Update(opening))
                {
                    list.Refresh(); // rebuilds the visible rows (never during the tree update: this runs first)
                }
                else
                {
                    rows.Refresh(opening);
                }

                selected?.Sync();
            }

            public void Rebuild()
            {
                source?.Invalidate();
                source?.Update(opening: true);
                list.Refresh();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Selection bindings (BindSelected / BindSelection)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Two-way sync between an element's selection and a state value, run every tick: a state change (an action,
        /// another input) is pushed into the element, a user selection is written into state.
        /// </summary>
        private sealed class SelectionBinding
        {
            private readonly StateAddress address;
            private readonly DataStateStore store;
            private readonly Func<string> read;
            private readonly Action<string> write;
            private string? lastState;
            private string? lastElement;

            private SelectionBinding(StateAddress address, DataStateStore store, Func<string> read, Action<string> write)
            {
                this.address = address;
                this.store = store;
                this.read = read;
                this.write = write;
            }

            internal static SelectionBinding? Create(string? key, DataScope scope, DataPath path, DataMessageLog log, DataStateStore store, Func<string> read, Action<string> write)
            {
                if (key == null)
                {
                    return null;
                }

                if (!StateAddress.TryParse(key, scope, allowBare: true, out StateAddress address, out string error))
                {
                    log.Error(path, error);
                    return null;
                }

                if (address.Scope == StateScope.Stat)
                {
                    log.Warn(path, "a selection cannot be bound to stat.* values; it is ignored.");
                    return null;
                }

                return new SelectionBinding(address, store, read, write);
            }

            internal void Sync()
            {
                string state = store.Read(address).AsString();
                if (state != lastState)
                {
                    lastState = state;
                    write(state);
                    lastElement = read();
                    return;
                }

                string element = read();
                if (element != lastElement)
                {
                    lastElement = element;
                    if (!store.Write(address, StateAddress.Infer(element), out string error))
                    {
                        UIServices.Log($"could not write the selection into {address}: {error}", LogLevel.Warn);
                    }

                    lastState = store.Read(address).AsString();
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Dropdown with ChoicesSource
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A dropdown whose choices (values and labels) come from a source; they follow the source as it changes.</summary>
        private IUIElement CreateSourcedDropdown(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            SourceBinding? source = SourceBinding.Create(def.ChoicesSource, scope, def.As, ctx.Runtime, path.Field("ChoicesSource"), ctx.Log);
            var choices = new SourcedChoices(source, def.ChoiceValue, def.ChoiceLabel, resolver);
            choices.Refresh(opening: true);
            Bridge.BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Text, choices.Values.Length > 0 ? choices.Values[0] : string.Empty, DataValue.FromString);
            IUIDropdown dropdown = ctx.Api.AddDropdown(parent, ctx.IdOf(def), () => choices.Values, () => choices.Labels, () => target.Read().AsString(), v => Write(scope, target, DataValue.FromString(v ?? string.Empty)));
            ctx.Applier.AddRefresher(choices);
            return dropdown;
        }

        /// <summary>The value / label arrays of a dropdown's source, recomputed only when the rows change.</summary>
        private sealed class SourcedChoices : IDataRefresher
        {
            private readonly SourceBinding? source;
            private readonly string? valueExpression;
            private readonly string? labelExpression;
            private readonly IValueResolver resolver;

            internal SourcedChoices(SourceBinding? source, string? valueExpression, string? labelExpression, IValueResolver resolver)
            {
                this.source = source;
                this.valueExpression = valueExpression;
                this.labelExpression = labelExpression;
                this.resolver = resolver;
            }

            internal string[] Values { get; private set; } = Array.Empty<string>();
            internal string[] Labels { get; private set; } = Array.Empty<string>();

            public void Refresh(bool opening)
            {
                if (source == null || (!source.Update(opening) && Values.Length == source.Count && !opening))
                {
                    return;
                }

                int count = source.Count;
                var values = new string[count];
                var labels = new string[count];
                for (int i = 0; i < count; i++)
                {
                    DataValue row = source.Row(i);
                    DataScope rowScope = source.ScopeFor(i);
                    values[i] = valueExpression != null ? resolver.Evaluate(valueExpression, rowScope, out _).AsString() : Field(row, "value", "id", "key") ?? row.AsString();
                    labels[i] = labelExpression != null ? resolver.Evaluate(labelExpression, rowScope, out _).AsString() : Field(row, "label", "displayName", "name") ?? values[i];
                }

                Values = values;
                Labels = labels;
            }

            private static string? Field(DataValue row, params string[] names)
            {
                foreach (string name in names)
                {
                    if (row.Kind == DataKind.Object && row.TryGetMember(name, out DataValue value) && !value.IsNull)
                    {
                        return value.AsString();
                    }
                }

                return null;
            }
        }
    }
}
