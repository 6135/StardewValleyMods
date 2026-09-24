using System.Collections.Generic;
using UIFramework.Api;
using UIFramework.Components;

namespace UIFramework.Core
{
    /// <summary>
    /// The player-visible state of a menu captured by element id before <see cref="UIMenu.RebuildInPlace"/> and put back
    /// on the rebuilt tree: focus, scroll offsets (including the menu's own viewport), list position / selection and
    /// data grid position / sort / selection / column widths. Elements whose id no longer exists are skipped.
    /// </summary>
    internal sealed class MenuViewState
    {
        private string? focusedId;
        private readonly Dictionary<string, int> scrollOffsets = new();
        private readonly Dictionary<string, (int First, int Selected)> lists = new();
        private readonly Dictionary<string, GridState> grids = new();

        private sealed class GridState
        {
            internal int FirstVisible;
            internal string? SortColumn;
            internal bool SortDescending;
            internal int[] SelectedRows = System.Array.Empty<int>();
            internal readonly Dictionary<string, string> ColumnWidths = new();
        }

        /// <summary>Capture the state of <paramref name="menu"/>'s tree.</summary>
        internal static MenuViewState Capture(UIMenu menu)
        {
            var state = new MenuViewState { focusedId = menu.Focus.Focused?.Id };
            foreach (UIElement element in menu.Viewport.SelfAndDescendants())
            {
                state.CaptureElement(element);
            }

            return state;
        }

        private void CaptureElement(UIElement element)
        {
            switch (element)
            {
                case ScrollView scroll:
                    scrollOffsets[scroll.Id] = scroll.ScrollOffset;
                    break;
                case IUIList list:
                    lists[element.Id] = (list.FirstVisibleIndex, list.Selectable ? list.SelectedIndex : -1);
                    break;
                case IUIDataGrid grid:
                    var g = new GridState
                    {
                        FirstVisible = grid.FirstVisibleIndex,
                        SortColumn = grid.SortColumn,
                        SortDescending = grid.SortDescending,
                        SelectedRows = grid.Selectable ? grid.SelectedRows ?? System.Array.Empty<int>() : System.Array.Empty<int>()
                    };
                    for (int i = 0; i < grid.ColumnCount; i++)
                    {
                        IUIDataGridColumn column = grid.GetColumn(i);
                        g.ColumnWidths[column.Id] = column.Width;
                    }

                    grids[element.Id] = g;
                    break;
            }
        }

        /// <summary>Put the captured state back on <paramref name="menu"/>'s (rebuilt, laid out) tree.</summary>
        internal void Restore(UIMenu menu)
        {
            foreach (UIElement element in menu.Viewport.SelfAndDescendants())
            {
                RestoreElement(element);
            }

            if (focusedId != null && menu.IsOpen)
            {
                UIElement? focus = menu.Root.FindById(focusedId);
                if (focus != null && focus.Focusable && focus.Visible && focus.Enabled)
                {
                    menu.Focus.SetFocus(focus);
                }
            }
        }

        private void RestoreElement(UIElement element)
        {
            switch (element)
            {
                case ScrollView scroll when scrollOffsets.TryGetValue(scroll.Id, out int offset):
                    scroll.ScrollOffset = offset;
                    break;
                case IUIList list when lists.TryGetValue(element.Id, out var l):
                    list.FirstVisibleIndex = l.First;
                    if (list.Selectable && l.Selected >= 0 && l.Selected < list.ItemCount)
                    {
                        list.SelectedIndex = l.Selected;
                    }

                    break;
                case IUIDataGrid grid when grids.TryGetValue(element.Id, out GridState? g):
                    foreach ((string columnId, string width) in g.ColumnWidths)
                    {
                        IUIDataGridColumn? column = grid.FindColumn(columnId);
                        if (column != null)
                        {
                            column.Width = width;
                        }
                    }

                    if (g.SortColumn != null && grid.FindColumn(g.SortColumn) != null)
                    {
                        grid.Sort(g.SortColumn, g.SortDescending);
                    }

                    grid.FirstVisibleIndex = g.FirstVisible;
                    if (grid.Selectable && g.SelectedRows.Length > 0)
                    {
                        grid.SelectedRows = g.SelectedRows;
                    }

                    break;
            }
        }
    }
}
