using System;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// One column of a <see cref="DataGrid"/>: header, track width, sort / resize flags and the per-row delegates.
    /// Changing a layout member re-arranges the grid; changing a content member rebuilds the visible rows.
    /// </summary>
    internal sealed class DataGridColumn : IUIDataGridColumn
    {
        private readonly DataGrid owner;
        private Func<string>? header;
        private string width;
        private int minWidth = 24;
        private UIAlign align = UIAlign.Start;
        private Func<int, string>? text;
        private Func<int, string>? sortKey;
        private Func<int, double>? sortNumber;
        private Action<int, IUIContainer>? buildCell;
        private Func<int, string>? cellTooltip;

        internal DataGridColumn(DataGrid owner, string id, Func<string>? header, string? width)
        {
            this.owner = owner;
            Id = id;
            this.header = header;
            this.width = string.IsNullOrWhiteSpace(width) ? "*" : width;
            Track = LayoutEngine.ParseTracks(this.width)[0];
        }

        public string Id { get; }

        /// <summary>Parsed <see cref="Width"/>.</summary>
        internal GridTrack Track { get; private set; }

        /// <summary>Absolute X of the column after layout.</summary>
        internal int ResolvedX { get; set; }

        /// <summary>Width in UI pixels after layout.</summary>
        internal int ResolvedWidth { get; set; }

        /// <summary>Absolute right edge after layout.</summary>
        internal int ResolvedRight => ResolvedX + ResolvedWidth;

        // ---------------------------------------------------------------------------------------------------------
        //  Properties
        // ---------------------------------------------------------------------------------------------------------

        Func<string> IUIDataGridColumn.Header { get => header!; set => header = value; }

        public string Width
        {
            get => width;
            set => SetWidth(value);
        }

        public bool Sortable { get; set; }

        public bool Resizable { get; set; }

        public int MinWidth
        {
            get => minWidth;
            set
            {
                minWidth = Math.Max(0, value);
                owner.InvalidateLayout();
            }
        }

        public UIAlign Align
        {
            get => align;
            set
            {
                align = value;
                owner.RequestRefresh();
            }
        }

        internal Func<int, string>? TextFunc => text;

        Func<int, string> IUIDataGridColumn.Text
        {
            get => text!;
            set
            {
                text = value;
                owner.RequestRefresh();
            }
        }

        internal Func<int, string>? SortKeyFunc => sortKey;

        Func<int, string> IUIDataGridColumn.SortKey
        {
            get => sortKey!;
            set
            {
                sortKey = value;
                owner.RequestRefresh();
            }
        }

        internal Func<int, double>? SortNumberFunc => sortNumber;

        Func<int, double> IUIDataGridColumn.SortNumber
        {
            get => sortNumber!;
            set
            {
                sortNumber = value;
                owner.RequestRefresh();
            }
        }

        internal Action<int, IUIContainer>? BuildCellFunc => buildCell;

        Action<int, IUIContainer> IUIDataGridColumn.BuildCell
        {
            get => buildCell!;
            set
            {
                buildCell = value;
                owner.RequestRefresh();
            }
        }

        internal Func<int, string>? CellTooltipFunc => cellTooltip;

        Func<int, string> IUIDataGridColumn.CellTooltip
        {
            get => cellTooltip!;
            set
            {
                cellTooltip = value;
                owner.RequestRefresh();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Header text right now (guarded).</summary>
        internal string HeaderText => owner.GuardColumn(this, "Header", header, string.Empty) ?? string.Empty;

        /// <summary>Set the track from a string (<c>auto</c>, <c>120px</c>, <c>*</c>) and re-arrange the grid.</summary>
        private void SetWidth(string? value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "*" : value;
            if (width == value)
            {
                return;
            }

            width = value;
            Track = LayoutEngine.ParseTracks(value)[0];
            owner.InvalidateLayout();
        }

        /// <summary>Pin the column to a pixel width (drag resize).</summary>
        internal void SetPixelWidth(int pixels)
        {
            SetWidth(Math.Max(minWidth, pixels).ToString(System.Globalization.CultureInfo.InvariantCulture) + "px");
        }
    }
}
