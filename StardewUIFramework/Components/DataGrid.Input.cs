using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// Input half of <see cref="DataGrid"/>: header clicks (sort / resize drag), scrollbar, row clicks with
    /// single / multi selection, double-click and keyboard navigation while the grid is focused.
    /// </summary>
    internal sealed partial class DataGrid
    {
        /// <summary>Half-width of the divider hit zone used for column resizing.</summary>
        private const int ResizeGrip = 6;

        private const double DoubleClickMs = 400;

        private bool selectable;
        private bool multiSelect;
        private int primaryRow = -1;
        private int anchorRow = -1;
        private bool draggingThumb;
        private int resizingColumn = -1;
        private int resizeStartX;
        private int resizeStartWidth;
        private int resizeWidth;
        private int lastClickRow = -1;
        private double lastClickMs;

        /// <summary>Reads the Shift / Ctrl state at click time (replaceable by tests).</summary>
        internal static Func<(bool shift, bool ctrl)> ReadModifiers { get; set; } = () =>
        {
            KeyboardState kb = Game1.GetKeyboardState();
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            return (shift, ctrl);
        };

        // ---------------------------------------------------------------------------------------------------------
        //  Selection properties
        // ---------------------------------------------------------------------------------------------------------

        public bool Selectable
        {
            get => selectable;
            set => selectable = value;
        }

        public bool MultiSelect
        {
            get => multiSelect;
            set => multiSelect = value;
        }

        /// <summary>Primary selected underlying row or -1. Setting it replaces the selection without raising events.</summary>
        public int SelectedRow
        {
            get => primaryRow;
            set
            {
                selected.Clear();
                primaryRow = value < 0 ? -1 : value;
                anchorRow = primaryRow;
                if (primaryRow >= 0)
                {
                    selected.Add(primaryRow);
                }
            }
        }

        /// <summary>Selected underlying rows in display order. Setting it replaces the selection without raising events.</summary>
        public int[] SelectedRows
        {
            get => SelectionInDisplayOrder(selected);
            set
            {
                selected.Clear();
                foreach (int row in value ?? Array.Empty<int>())
                {
                    if (row >= 0)
                    {
                        selected.Add(row);
                    }
                }

                primaryRow = FirstInDisplayOrder(selected);
                anchorRow = primaryRow;
            }
        }

        public void ClearSelection()
        {
            selected.Clear();
            primaryRow = -1;
            anchorRow = -1;
        }

        /// <summary>Cursor position of the owning menu (0,0 while detached).</summary>
        private int CursorX => OwnerMenu?.CursorX ?? 0;

        private int CursorY => OwnerMenu?.CursorY ?? 0;

        /// <summary>Header column under the cursor while this grid is hovered, or -1.</summary>
        private int HoveredHeaderColumn => IsHovered && HeaderRect.Contains(CursorX, CursorY) ? ColumnAt(CursorX) : -1;

        // ---------------------------------------------------------------------------------------------------------
        //  Selection model (underlying indices)
        // ---------------------------------------------------------------------------------------------------------

        private int[] SelectionInDisplayOrder(HashSet<int> set)
        {
            var result = new List<int>(set.Count);
            foreach (int row in order)
            {
                if (set.Contains(row))
                {
                    result.Add(row);
                }
            }
            // rows outside the current order (filtered out) keep their selection but come last
            foreach (int row in set)
            {
                if (row >= position.Length || position[row] < 0)
                {
                    result.Add(row);
                }
            }
            return result.ToArray();
        }

        private int FirstInDisplayOrder(HashSet<int> set)
        {
            foreach (int row in order)
            {
                if (set.Contains(row))
                {
                    return row;
                }
            }
            return -1;
        }

        /// <summary>Drop selected rows past the new count.</summary>
        private void PruneSelection(int count)
        {
            selected.RemoveWhere(row => row >= count);
            if (primaryRow >= count)
            {
                primaryRow = FirstInDisplayOrder(selected);
            }

            if (anchorRow >= count)
            {
                anchorRow = primaryRow;
            }
        }

        /// <summary>Replace the selection from a user action; plays the select sound and raises <see cref="OnValueChanged"/> when it changed.</summary>
        private void CommitSelection(HashSet<int> set, int primary)
        {
            if (primaryRow == primary && selected.SetEquals(set))
            {
                return;
            }

            int old = primaryRow;
            selected.Clear();
            selected.UnionWith(set);
            primaryRow = primary;
            UIServices.PlaySound(SelectSound ?? Style.ClickSound ?? DefaultSelectSound);
            if (OnValueChanged != null)
            {
                Action<IUIValueEvent> cb = OnValueChanged;
                UIValueEvent e = UIValueEvent.Index(this, old, primary, old.ToString(), primary.ToString());
                Raise("OnValueChanged", () => cb(e));
            }
        }

        private void SelectSingle(int row)
        {
            anchorRow = row;
            CommitSelection(new HashSet<int> { row }, row);
        }

        private void ToggleRow(int row)
        {
            var set = new HashSet<int>(selected);
            if (!set.Remove(row))
            {
                set.Add(row);
            }

            anchorRow = row;
            CommitSelection(set, set.Contains(row) ? row : FirstInDisplayOrder(set));
        }

        /// <summary>Select every row displayed between the anchor and <paramref name="row"/> (inclusive).</summary>
        private void SelectRange(int anchor, int row)
        {
            int a = anchor >= 0 && anchor < position.Length ? position[anchor] : -1;
            int b = row >= 0 && row < position.Length ? position[row] : -1;
            if (a < 0 || b < 0)
            {
                SelectSingle(row);
                return;
            }

            var set = new HashSet<int>();
            for (int p = Math.Min(a, b); p <= Math.Max(a, b); p++)
            {
                set.Add(order[p]);
            }

            CommitSelection(set, row);
        }

        /// <summary>Apply a left click on a row: Ctrl toggles, Shift ranges (multi-select only), otherwise single.</summary>
        private void ApplySelectionClick(int row, bool shift, bool ctrl)
        {
            if (multiSelect && ctrl)
            {
                ToggleRow(row);
            }
            else if (multiSelect && shift && anchorRow >= 0)
            {
                SelectRange(anchorRow, row);
            }
            else
            {
                SelectSingle(row);
            }
        }

        /// <summary>Raise <see cref="OnRowActivated"/> for an underlying row.</summary>
        private void ActivateRow(int row)
        {
            if (OnRowActivated != null)
            {
                Action<int> cb = OnRowActivated;
                Raise("OnRowActivated", () => cb(row));
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Geometry helpers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Column whose header spans <paramref name="px"/>, or -1.</summary>
        private int ColumnAt(int px)
        {
            for (int j = 0; j < columns.Count; j++)
            {
                if (px >= columns[j].ResolvedX && px < columns[j].ResolvedRight)
                {
                    return j;
                }
            }
            return -1;
        }

        /// <summary>Resizable column whose right divider is within <see cref="ResizeGrip"/> of <paramref name="px"/>, or -1.</summary>
        private int ResizeColumnAt(int px)
        {
            for (int j = 0; j < columns.Count; j++)
            {
                if (columns[j].Resizable && Math.Abs(px - columns[j].ResolvedRight) <= ResizeGrip)
                {
                    return j;
                }
            }
            return -1;
        }

        /// <summary>Underlying row shown at the point, or -1.</summary>
        private int RowAt(int px, int py)
        {
            if (!RowsRect.Contains(px, py))
            {
                return -1;
            }

            int slot = (py - RowsRect.Y) / EffectiveRowHeight;
            return slot >= 0 && slot < rows.Count ? rows[slot].Item : -1;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Mouse
        // ---------------------------------------------------------------------------------------------------------

        protected internal override bool HandleClick(UIClickEvent e)
        {
            if (e.Target == this && e.Button == UIMouseButton.Left && HandleOwnClick(e.X, e.Y))
            {
                return true;
            }

            // a click on a row (or bubbling up from anything inside it) acts on that row's item
            int row = RowAt(e.X, e.Y);
            if (row >= 0)
            {
                ClickRow(row, e);
            }

            return base.HandleClick(e);
        }

        /// <summary>Scrollbar parts, then the header (resize grip before sort). Returns false when nothing interactive of the grid's own was hit.</summary>
        private bool HandleOwnClick(int px, int py)
        {
            if (ScrollbarVisible && HandleScrollbarClick(px, py))
            {
                return true;
            }

            if (!HeaderRect.Contains(px, py))
            {
                return false;
            }

            int grip = ResizeColumnAt(px);
            if (grip >= 0)
            {
                resizingColumn = grip;
                resizeStartX = px;
                resizeStartWidth = columns[grip].ResolvedWidth;
                resizeWidth = resizeStartWidth;
                return true;
            }

            int column = ColumnAt(px);
            if (column >= 0 && columns[column].Sortable)
            {
                ToggleSort(columns[column]);
                return true;
            }
            return false;
        }

        /// <summary>Header click: ascending → descending on the same column, ascending on a new one.</summary>
        private void ToggleSort(DataGridColumn column)
        {
            bool descending = column.Id == sortColumn && !sortDescending;
            Sort(column.Id, descending);
            UIServices.PlaySound(SortSound ?? DefaultSortSound);
        }

        /// <summary>Arrows step one row, the thumb starts a drag, the track jumps. Returns false when no scrollbar part was hit.</summary>
        private bool HandleScrollbarClick(int px, int py)
        {
            switch (scrollbar.HitTest(px, py))
            {
                case ScrollbarGadget.Part.UpArrow:
                    ScrollWithSound(-1);
                    return true;
                case ScrollbarGadget.Part.DownArrow:
                    ScrollWithSound(1);
                    return true;
                case ScrollbarGadget.Part.Thumb:
                    draggingThumb = true;
                    return true;
                case ScrollbarGadget.Part.Track:
                    draggingThumb = true;
                    SetFirstVisibleFromY(py);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Scroll by <paramref name="delta"/> rows with the scroll sound when something moved.</summary>
        private void ScrollWithSound(int delta)
        {
            if (SetFirstVisible(firstVisible + delta))
            {
                UIServices.PlaySound(ScrollSound ?? DefaultScrollSound);
            }
        }

        /// <summary>Scroll so the thumb's center follows the cursor (thumb drag / track click).</summary>
        private void SetFirstVisibleFromY(int py)
        {
            int target = (int)Math.Round(scrollbar.FractionFromY(py) * MaxFirstIndex);
            if (SetFirstVisible(target))
            {
                UIServices.PlaySound(ScrollSound ?? DefaultScrollSound);
            }
        }

        /// <summary>Focus the grid, apply selection (left button), raise <see cref="OnRowClick"/>, detect a double-click.</summary>
        private void ClickRow(int row, UIClickEvent e)
        {
            if (OwnerMenu?.Focus.Focused?.IsSelfOrDescendantOf(this) != true)
            {
                Focus();
            }

            if (selectable && e.Button == UIMouseButton.Left)
            {
                (bool shift, bool ctrl) = ReadModifiers();
                ApplySelectionClick(row, shift, ctrl);
            }

            if (OnRowClick != null)
            {
                Action<IUIRowEvent> cb = OnRowClick;
                var rowEvent = new UIRowEvent(this, e, row);
                Raise("OnRowClick", () => cb(rowEvent));
                e.Handled |= rowEvent.Handled;
            }

            if (e.Button == UIMouseButton.Left)
            {
                DetectDoubleClick(row);
            }
        }

        /// <summary>Two left clicks on the same row within <see cref="DoubleClickMs"/> activate it.</summary>
        private void DetectDoubleClick(int row)
        {
            double now = UIServices.NowMs();
            if (row == lastClickRow && now - lastClickMs <= DoubleClickMs)
            {
                lastClickRow = -1;
                ActivateRow(row);
                return;
            }

            lastClickRow = row;
            lastClickMs = now;
        }

        protected internal override void HandleClickHeld(int px, int py)
        {
            if (draggingThumb)
            {
                SetFirstVisibleFromY(py);
            }
            else if (resizingColumn >= 0)
            {
                DataGridColumn column = columns[resizingColumn];
                resizeWidth = Math.Max(column.MinWidth, resizeStartWidth + (px - resizeStartX));
                column.SetPixelWidth(resizeWidth);
            }
            else
            {
                // plain click, nothing to drag
            }
        }

        protected internal override void HandleClickRelease(int px, int py)
        {
            draggingThumb = false;
            if (resizingColumn < 0)
            {
                return;
            }

            DataGridColumn column = columns[resizingColumn];
            resizingColumn = -1;
            if (resizeWidth != resizeStartWidth && OnColumnResized != null)
            {
                Action<string, int> cb = OnColumnResized;
                int width = resizeWidth;
                Raise("OnColumnResized", () => cb(column.Id, width));
            }
        }

        /// <summary>Wheel: one row per notch; unhandled (falls through) when already at the end.</summary>
        protected internal override bool HandleScroll(int direction)
        {
            if (direction == 0 || !SetFirstVisible(firstVisible + (direction > 0 ? -1 : 1)))
            {
                return false;
            }

            UIServices.PlaySound(ScrollSound ?? DefaultScrollSound);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keyboard
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Up / Down / PageUp / PageDown / Home / End move the selection while the grid itself is focused (after the consumer's <c>OnKey</c>).</summary>
        protected internal override bool HandleKey(UIKeyEvent e)
        {
            if (base.HandleKey(e) || !IsFocused || !selectable)
            {
                return e.Handled;
            }

            switch (e.Key)
            {
                case Keys.Up:
                    MoveSelection(-1, e.Shift);
                    break;
                case Keys.Down:
                    MoveSelection(1, e.Shift);
                    break;
                case Keys.PageUp:
                    MoveSelection(-visibleRows, e.Shift);
                    break;
                case Keys.PageDown:
                    MoveSelection(visibleRows, e.Shift);
                    break;
                case Keys.Home:
                    MoveSelectionTo(0, e.Shift);
                    break;
                case Keys.End:
                    MoveSelectionTo(order.Length - 1, e.Shift);
                    break;
                default:
                    return false;
            }

            e.Handled = true;
            return true;
        }

        /// <summary>Move the primary selection by <paramref name="delta"/> display positions.</summary>
        private void MoveSelection(int delta, bool extend)
        {
            int current = primaryRow >= 0 && primaryRow < position.Length ? position[primaryRow] : -1;
            MoveSelectionTo(current < 0 ? 0 : current + delta, extend);
        }

        /// <summary>Select the row at a display position (Shift extends from the anchor in multi-select) and scroll it into view.</summary>
        private void MoveSelectionTo(int pos, bool extend)
        {
            EnsureFresh();
            if (order.Length == 0)
            {
                return;
            }

            pos = Math.Clamp(pos, 0, order.Length - 1);
            int row = order[pos];
            if (multiSelect && extend && anchorRow >= 0)
            {
                SelectRange(anchorRow, row);
            }
            else
            {
                SelectSingle(row);
            }

            EnsureVisible(pos);
        }

        /// <summary>Enter while focused activates the primary selected row.</summary>
        protected internal override bool HandleActivate()
        {
            if (primaryRow < 0 || !selected.Contains(primaryRow))
            {
                return false;
            }

            ActivateRow(primaryRow);
            return true;
        }
    }
}
