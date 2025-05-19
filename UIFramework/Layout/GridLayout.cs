using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using UIFramework.Components.Base;

namespace UIFramework.Layout
{
    public class GridLayout
    {
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
        public int HorizontalSpacing { get; set; } = 0;
        public int VerticalSpacing { get; set; } = 0;

        private readonly Dictionary<string, (int Column, int Row, int ColumnSpan, int RowSpan)> _componentPositions;
        private readonly List<BaseComponent> _components;

        public GridLayout(int columns, int rows, int cellWidth, int cellHeight)
        {
            Columns = columns;
            Rows = rows;
            CellWidth = cellWidth;
            CellHeight = cellHeight;
            _componentPositions = new Dictionary<string, (int, int, int, int)>();
            _components = new List<BaseComponent>();
        }

        public Vector2 GetCellPosition(int column, int row)
        {
            if (column < 0 || column >= Columns || row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException($"Position ({column}, {row}) is outside the grid bounds.");

            float x = column * (CellWidth + HorizontalSpacing);
            float y = row * (CellHeight + VerticalSpacing);

            return new Vector2(x, y);
        }

        public void AddComponent(BaseComponent component, int column, int row, int columnSpan = 1, int rowSpan = 1)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            if (column < 0 || column + columnSpan > Columns || row < 0 || row + rowSpan > Rows)
                throw new ArgumentOutOfRangeException($"Position ({column}, {row}) with span ({columnSpan}, {rowSpan}) is outside the grid bounds.");

            if (columnSpan <= 0 || rowSpan <= 0)
                throw new ArgumentException("Column span and row span must be positive.");

            // Remove component if it already exists in the grid
            if (_componentPositions.ContainsKey(component.Id))
            {
                RemoveComponent(component.Id);
            }

            // Add component to the grid
            _componentPositions[component.Id] = (column, row, columnSpan, rowSpan);
            _components.Add(component);

            // Calculate and set the component's position and size
            Vector2 position = GetCellPosition(column, row);
            int width = (columnSpan * CellWidth) + (HorizontalSpacing * (columnSpan - 1));
            int height = (rowSpan * CellHeight) + (VerticalSpacing * (rowSpan - 1));

            component.Position = position;
            component.Size = new Vector2(width, height);
        }

        public void RemoveComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return;

            if (_componentPositions.TryGetValue(componentId, out _))
            {
                _componentPositions.Remove(componentId);
                _components.RemoveAll(c => c.Id == componentId);
            }
        }

        public void ResizeGrid(int newColumns, int newRows, int newCellWidth, int newCellHeight)
        {
            // Update grid properties
            Columns = newColumns;
            Rows = newRows;
            CellWidth = newCellWidth;
            CellHeight = newCellHeight;

            // Reposition and resize all components
            foreach (var component in _components)
            {
                if (_componentPositions.TryGetValue(component.Id, out var position))
                {
                    var (column, row, columnSpan, rowSpan) = position;

                    // Check if the component is still within the grid bounds
                    if (column + columnSpan > newColumns || row + rowSpan > newRows)
                    {
                        // Adjust the span if needed
                        int adjustedColumnSpan = Math.Min(columnSpan, newColumns - column);
                        int adjustedRowSpan = Math.Min(rowSpan, newRows - row);

                        if (adjustedColumnSpan <= 0 || adjustedRowSpan <= 0)
                        {
                            // Component is now outside the grid
                            _componentPositions.Remove(component.Id);
                            _components.Remove(component);
                            continue;
                        }

                        // Update position info with adjusted spans
                        _componentPositions[component.Id] = (column, row, adjustedColumnSpan, adjustedRowSpan);
                    }

                    // Recalculate position and size
                    Vector2 newPosition = GetCellPosition(column, row);
                    int width = (columnSpan * CellWidth) + (HorizontalSpacing * (columnSpan - 1));
                    int height = (rowSpan * CellHeight) + (VerticalSpacing * (rowSpan - 1));

                    component.Position = newPosition;
                    component.Size = new Vector2(width, height);
                }
            }
        }

        public void SetSpacing(int horizontal, int vertical)
        {
            HorizontalSpacing = horizontal;
            VerticalSpacing = vertical;

            // Update all component positions and sizes
            foreach (var component in _components)
            {
                if (_componentPositions.TryGetValue(component.Id, out var position))
                {
                    var (column, row, columnSpan, rowSpan) = position;
                    Vector2 newPosition = GetCellPosition(column, row);
                    int width = (columnSpan * CellWidth) + (HorizontalSpacing * (columnSpan - 1));
                    int height = (rowSpan * CellHeight) + (VerticalSpacing * (rowSpan - 1));

                    component.Position = newPosition;
                    component.Size = new Vector2(width, height);
                }
            }
        }

        public void Clear()
        {
            _componentPositions.Clear();
            _components.Clear();
        }

        public IReadOnlyCollection<BaseComponent> GetComponents()
        {
            return _components.AsReadOnly();
        }

        public (int Column, int Row, int ColumnSpan, int RowSpan)? GetComponentPosition(string componentId)
        {
            if (_componentPositions.TryGetValue(componentId, out var position))
            {
                return position;
            }
            return null;
        }
    }
}