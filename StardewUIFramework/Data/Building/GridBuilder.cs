using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// <summary>
    /// <c>DataGrid</c>: every <see cref="IUIDataGrid"/> member as data. Column values (<c>Text</c>, <c>SortKey</c>,
    /// <c>SortNumber</c>, <c>Tooltip</c>) are expressions over the row, compiled once and evaluated in the row's scope
    /// (<c>row.x</c>, the <c>As</c> name, <c>index</c>); a column <c>Cell</c> template is built into the cell container with
    /// ids prefixed by the cell's id. <c>Filter</c> hides rows while keeping indices stable and is re-checked when state
    /// changes; <c>BindSelected</c> / <c>BindSelection</c> sync the selection with state; row events get
    /// <c>event.row</c> / <c>event.index</c>.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        private IUIElement CreateDataGrid(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            PropertyApplier a = ctx.Applier;
            int rowHeight = Math.Max(1, a.Initial(def.RowHeight, ValueParsers.Int, DefaultRowHeight, scope, path.Field("RowHeight")));
            int visibleRows = Math.Max(1, a.Initial(def.VisibleRows, ValueParsers.Int, DefaultVisibleRows, scope, path.Field("VisibleRows")));
            SourceBinding? source = CreateSource(ctx, def.Source, scope, def.As, path.Field("Source"), "a DataGrid");
            source?.Update(opening: true);
            IUIDataGrid grid = ctx.Api.AddDataGrid(parent, ctx.IdOf(def), rowHeight, visibleRows, () => source?.Count ?? 0);
            Func<int, DataScope> rowScope = index => source?.ScopeFor(index) ?? RowScope.For(scope, DataValue.Null, index, def.As);

            var cells = new List<TemplateRows>();
            if (def.Columns == null || def.Columns.Count == 0)
            {
                ctx.Log.Warn(path.Field("Columns"), "a DataGrid needs Columns ([{ \"Id\": \"name\", \"Header\": \"Name\", \"Text\": \"${row.name}\" }, ...]).");
            }
            else
            {
                for (int i = 0; i < def.Columns.Count; i++)
                {
                    ColumnDefinition? column = def.Columns[i];
                    if (column == null || string.IsNullOrWhiteSpace(column.Id))
                    {
                        continue; // reported by the validator
                    }

                    TemplateRows? cellRows = AddColumn(ctx, grid, column, scope, rowScope, source, path.Field("Columns").Index(i, column.Id));
                    if (cellRows != null)
                    {
                        cells.Add(cellRows);
                    }
                }
            }

            // initial sort ("profit" or "profit desc"); a rebuild keeps the player's sort (view state)
            if (!string.IsNullOrWhiteSpace(def.Sort))
            {
                string[] parts = def.Sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool descending = a.Initial(def.SortDescending, ValueParsers.Bool, false, scope, path.Field("SortDescending"))
                    || (parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase));
                if (grid.FindColumn(parts[0]) == null)
                {
                    ctx.Log.Warn(path.Field("Sort"), $"no column has the id '{parts[0]}'; the rows are not sorted.");
                }
                else
                {
                    grid.Sort(parts[0], descending);
                }
            }

            // filter: a row predicate (underlying indices stay stable); re-checked when the state changes
            GridFilter? filter = null;
            ValueSource<bool>? predicate = a.Source(def.Filter, ValueParsers.Bool, path.Field("Filter"));
            if (predicate != null)
            {
                filter = new GridFilter(predicate, rowScope, () => source?.Count ?? 0);
                grid.Filter = filter.Matches;
            }

            // rich row tooltips
            if (def.RowTooltip != null)
            {
                CompiledTooltip? tooltip = CompileTooltip(def.RowTooltip, scope.Owner, path.Field("RowTooltip"), a);
                if (tooltip != null)
                {
                    grid.RowTooltip = index => tooltip.Create(rowScope(index));
                }
            }

            var element = (UIElement)grid;
            SelectionBinding? selected = SelectionBinding.Create(def.BindSelected, scope, path.Field("BindSelected"), ctx.Log, store,
                () => grid.SelectedRow.ToString(CultureInfo.InvariantCulture),
                text => grid.SelectedRow = ValueParsers.TryParseInt(text, out int row) ? row : -1);
            SelectionBinding? selection = SelectionBinding.Create(def.BindSelection, scope, path.Field("BindSelection"), ctx.Log, store,
                () => string.Join(",", grid.SelectedRows.Select(r => r.ToString(CultureInfo.InvariantCulture))),
                text => grid.SelectedRows = ParseRows(text));
            var collection = new GridCollection(grid, source, cells, filter, selected, selection);
            ctx.Runtime.Collections[element.Id] = collection;
            a.AddRefresher(collection);
            return grid;
        }

        /// <summary>Row indices from comma-separated text (invalid parts are skipped).</summary>
        private static int[] ParseRows(string text)
        {
            var rows = new List<int>();
            foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (ValueParsers.TryParseInt(part, out int row) && row >= 0)
                {
                    rows.Add(row);
                }
            }

            return rows.ToArray();
        }

        /// <summary>Add one data grid column; returns the cell template rows when the column has a <c>Cell</c>.</summary>
        private TemplateRows? AddColumn(BuildContext ctx, IUIDataGrid grid, ColumnDefinition def, DataScope scope, Func<int, DataScope> rowScope, SourceBinding? source, DataPath path)
        {
            PropertyApplier a = ctx.Applier;
            string id = def.Id!.Trim();
            string width = a.Initial(def.Width, ValueParsers.Text, "*", scope, path.Field("Width"));
            IUIDataGridColumn column = grid.AddColumn(id, a.Text(def.Header, scope, path.Field("Header")) ?? (() => id), width);
            if (def.Width != null && ExpressionValueResolver.HasTemplate(def.Width))
            {
                a.Apply(def.Width, ValueParsers.Text, scope, path.Field("Width"), v => column.Width = v);
            }

            a.Apply(def.MinWidth, ValueParsers.Int, scope, path.Field("MinWidth"), v => column.MinWidth = v);
            a.Apply(def.Align, ValueParsers.Align, scope, path.Field("Align"), v => column.Align = v);
            a.Apply(def.Sortable, ValueParsers.Bool, scope, path.Field("Sortable"), v => column.Sortable = v);
            a.Apply(def.Resizable, ValueParsers.Bool, scope, path.Field("Resizable"), v => column.Resizable = v);

            // text: the Text template, else the row's field named like the column
            ValueSource<string>? text = a.Source(def.Text, ValueParsers.Text, path.Field("Text"));
            if (text != null)
            {
                column.Text = index => text.Get(rowScope(index));
            }
            else if (def.Cell == null)
            {
                column.Text = index => source != null && source.Row(index).TryGetMember(id, out DataValue field) ? field.AsString() : string.Empty;
            }
            else
            {
                // cells only
            }

            // sort keys: expressions over the row
            if (def.SortNumber != null)
            {
                ValueSource<double>? number = a.Source(def.SortNumber, ValueParsers.Number, path.Field("SortNumber"));
                if (number != null)
                {
                    column.SortNumber = index => number.Get(rowScope(index));
                }
            }

            if (def.SortKey != null)
            {
                string key = def.SortKey;
                column.SortKey = index => resolver.Evaluate(key, rowScope(index), out _).AsString();
            }

            ValueSource<string>? tooltip = a.Source(def.Tooltip, ValueParsers.Text, path.Field("Tooltip"));
            if (tooltip != null)
            {
                column.CellTooltip = index => tooltip.Get(rowScope(index));
            }

            if (def.Cell == null)
            {
                return null;
            }

            var cells = new TemplateRows(this, ctx, def.Cell, path.Field("Cell"));
            column.BuildCell = (index, cell) => cells.Build(cell, rowScope(index));
            return cells;
        }

        /// <summary>A grid's row predicate; <see cref="Changed"/> tells whether any row's result moved since the last check.</summary>
        private sealed class GridFilter
        {
            private readonly ValueSource<bool> predicate;
            private readonly Func<int, DataScope> rowScope;
            private readonly Func<int> count;
            private bool[] last = Array.Empty<bool>();
            private long epoch = -1;

            internal GridFilter(ValueSource<bool> predicate, Func<int, DataScope> rowScope, Func<int> count)
            {
                this.predicate = predicate;
                this.rowScope = rowScope;
                this.count = count;
            }

            internal bool Matches(int index) => predicate.Get(rowScope(index));

            /// <summary>Re-evaluate every row when the state moved; true when a result changed.</summary>
            internal bool Changed()
            {
                long now = DataStateStore.Active?.Epoch(DataStateStore.Screen) ?? 0;
                if (now == epoch)
                {
                    return false;
                }

                epoch = now;
                int n = count();
                var results = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    results[i] = Matches(i);
                }

                bool changed = !results.AsSpan().SequenceEqual(last);
                last = results;
                return changed;
            }
        }

        /// <summary>Keeps a data grid in sync with its source, filter, cell templates and selection bindings.</summary>
        private sealed class GridCollection : IDataRefresher, IDataCollection
        {
            private readonly IUIDataGrid grid;
            private readonly SourceBinding? source;
            private readonly List<TemplateRows> cells;
            private readonly GridFilter? filter;
            private readonly SelectionBinding? selected;
            private readonly SelectionBinding? selection;

            internal GridCollection(IUIDataGrid grid, SourceBinding? source, List<TemplateRows> cells, GridFilter? filter, SelectionBinding? selected, SelectionBinding? selection)
            {
                this.grid = grid;
                this.source = source;
                this.cells = cells;
                this.filter = filter;
                this.selected = selected;
                this.selection = selection;
            }

            public UIElement Element => (UIElement)grid;

            internal SourceBinding? Source => source;

            public void Refresh(bool opening)
            {
                bool rows = source != null && source.Update(opening);
                bool filtered = filter != null && filter.Changed();
                if (rows || filtered)
                {
                    grid.Refresh(); // re-filters, re-sorts and rebuilds the visible rows (selection kept by index)
                }
                else
                {
                    foreach (TemplateRows cell in cells)
                    {
                        cell.Refresh(opening);
                    }
                }

                selected?.Sync();
                selection?.Sync();
            }

            public void Rebuild()
            {
                source?.Invalidate();
                source?.Update(opening: true);
                grid.Refresh();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Events (wired with the other element events)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Event fields of a row: <c>event.row</c> and <c>event.index</c> (underlying index).</summary>
        private static Dictionary<string, DataValue> RowFields(SourceBinding? source, int index)
        {
            return new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["row"] = source?.Row(index) ?? DataValue.Null,
                ["index"] = DataValue.FromNumber(index)
            };
        }

        /// <summary>The source of a collection built in this runtime (for its events), or null.</summary>
        private static SourceBinding? SourceOf(BuildContext ctx, UIElement element)
        {
            return ctx.Runtime.Collections.TryGetValue(element.Id, out IDataCollection? collection) ? collection switch
            {
                GridCollection grid => grid.Source,
                ListCollection list => list.Source,
                _ => null
            } : null;
        }

        /// <summary>The events of lists and grids: every handler runs in the row's scope with <c>event.row</c> / <c>event.index</c>.</summary>
        private void WireCollectionEvents(BuildContext ctx, IUIElement e, ElementDefinition def, DataScope scope)
        {
            SourceBinding? source = SourceOf(ctx, (UIElement)e);
            DataScope RowEventScope(int index, string name, object? args, Dictionary<string, DataValue>? extra = null)
            {
                Dictionary<string, DataValue> fields = RowFields(source, index);
                if (extra != null)
                {
                    foreach ((string key, DataValue value) in extra)
                    {
                        fields[key] = value;
                    }
                }

                DataScope rowScope = source != null && index >= 0 ? source.ScopeFor(index, scope) : scope;
                return rowScope.WithEvent(name, args, fields);
            }

            switch (e)
            {
                case IUIList list:
                    if (def.OnValueChanged is { Count: > 0 } listChanged)
                    {
                        list.OnValueChanged = v => DataActionRunner.Run(listChanged, RowEventScope(v.NewIndex, "OnValueChanged", v));
                    }

                    SetIf(DataActionRunner.Handler<int>(def.OnScroll, scope, "OnScroll"), h => list.OnScroll = h);
                    break;

                case IUIDataGrid grid:
                    if (def.OnValueChanged is { Count: > 0 } gridChanged)
                    {
                        grid.OnValueChanged = v => DataActionRunner.Run(gridChanged, RowEventScope(v.NewIndex, "OnValueChanged", v));
                    }

                    if (def.OnRowClick is { Count: > 0 } click)
                    {
                        grid.OnRowClick = r => DataActionRunner.Run(click, RowEventScope(r.Row, "OnRowClick", r));
                    }

                    if (def.OnRowActivated is { Count: > 0 } activated)
                    {
                        grid.OnRowActivated = row => DataActionRunner.Run(activated, RowEventScope(row, "OnRowActivated", null));
                    }

                    if (def.OnColumnResized is { Count: > 0 } resized)
                    {
                        grid.OnColumnResized = (column, width) => DataActionRunner.Run(resized, scope.WithEvent("OnColumnResized", null, new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["column"] = DataValue.FromString(column),
                            ["width"] = DataValue.FromNumber(width),
                            ["value"] = DataValue.FromNumber(width)
                        }));
                    }

                    SetIf(DataActionRunner.Handler<int>(def.OnScroll, scope, "OnScroll"), h => grid.OnScroll = h);
                    break;
            }
        }
    }
}
