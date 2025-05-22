using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using UIFramework.Components.Base;
using UIFramework.Menus;

namespace UIFramework.Layout
{
    /// <summary>
    /// A grid layout that manages component positions using a predefined 12×12 grid system.
    /// Components are dynamically sized based on the number of grid cells they occupy.
    /// </summary>
    public class GridLayout
    {
        // Fixed number of rows and columns for the grid system
        public const int GRID_COLUMNS = 12;

        public const int GRID_ROWS = 12;

        // Spacing between cells
        public int HorizontalSpacing { get; set; } = 5;

        public int VerticalSpacing { get; set; } = 5;

        // Base position of the grid
        public Vector2 Origin { get; private set; } = Vector2.Zero;

        // Menu this grid layout is associated with
        private BaseMenu _parentMenu;

        // Tracks component positions in the grid
        private readonly Dictionary<string, (int Column, int Row, int ColumnSpan, int RowSpan)> _componentPositions;

        private readonly List<BaseComponent> _components;

        public GridLayout(BaseMenu parentMenu)
        {
            _parentMenu = parentMenu ?? throw new ArgumentNullException(nameof(parentMenu));
            _componentPositions = new Dictionary<string, (int, int, int, int)>();
            _components = new List<BaseComponent>();

            // Set origin relative to menu position (accounting for menu title if present)
            UpdateOrigin();
        }

        /// <summary>
        /// Updates the origin point of the grid based on the parent menu's position
        /// </summary>
        public void UpdateOrigin()
        {
            if (_parentMenu == null) return;

            // Get menu bounds
            int xPos = _parentMenu.xPositionOnScreen;
            int yPos = _parentMenu.yPositionOnScreen;

            // Add title offset if menu has a title
            bool hasTitle = !string.IsNullOrEmpty(_parentMenu.Config?.Title);
            int titleOffset = hasTitle ? 50 : 20; // 50px for title, 20px for padding

            Origin = new Vector2(xPos + 20, yPos + titleOffset); // 20px margin from left

            // Update all component positions with the new origin
            UpdateComponentPositions();
        }

        /// <summary>
        /// Calculates the position of a grid cell
        /// </summary>
        public Vector2 GetCellPosition(int column, int row)
        {
            if (column < 0 || column >= GRID_COLUMNS || row < 0 || row >= GRID_ROWS)
                throw new ArgumentOutOfRangeException($"Position ({column}, {row}) is outside the grid bounds.");

            // Calculate cell dimensions based on menu size
            (float cellWidth, float cellHeight) = GetCellDimensions();

            float x = Origin.X + (column * (cellWidth + HorizontalSpacing));
            float y = Origin.Y + (row * (cellHeight + VerticalSpacing));

            return new Vector2(x, y);
        }

        /// <summary>
        /// Calculates the dimensions of a single grid cell based on menu size
        /// </summary>
        private (float Width, float Height) GetCellDimensions()
        {
            if (_parentMenu == null)
                return (0, 0);

            // Calculate available space accounting for margins and spacing
            float availableWidth = _parentMenu.width - 40 - (HorizontalSpacing * (GRID_COLUMNS - 1));
            float availableHeight = _parentMenu.height - 70 - (VerticalSpacing * (GRID_ROWS - 1));

            // If menu has a title, adjust available height
            if (!string.IsNullOrEmpty(_parentMenu.Config?.Title))
            {
                availableHeight -= 30; // Additional title space
            }

            // Calculate individual cell dimensions
            float cellWidth = availableWidth / GRID_COLUMNS;
            float cellHeight = availableHeight / GRID_ROWS;

            return (cellWidth, cellHeight);
        }

        /// <summary>
        /// Add a component to the grid layout
        /// </summary>
        public void AddComponent(BaseComponent component, int column, int row, int columnSpan = 1, int rowSpan = 1)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            if (column < 0 || column + columnSpan > GRID_COLUMNS || row < 0 || row + rowSpan > GRID_ROWS)
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

            // Position and size the component
            PositionComponent(component);

            // Add component to parent menu if it's not already there
            if (_parentMenu != null && _parentMenu.GetComponent(component.Id) == null)
            {
                _parentMenu.AddComponent(component);
            }
        }

        /// <summary>
        /// Positions and sizes a component based on its grid cell position and span
        /// </summary>
        private void PositionComponent(BaseComponent component)
        {
            if (!_componentPositions.TryGetValue(component.Id, out var position))
                return;

            var (column, row, columnSpan, rowSpan) = position;
            (float cellWidth, float cellHeight) = GetCellDimensions();

            // Calculate component position at the grid cell
            Vector2 cellPos = GetCellPosition(column, row);

            // Calculate component size based on the number of cells it spans
            float width = (columnSpan * cellWidth) + (HorizontalSpacing * (columnSpan - 1));
            float height = (rowSpan * cellHeight) + (VerticalSpacing * (rowSpan - 1));

            // Set component position and size
            component.Position = cellPos;
            component.Size = new Vector2(width, height);
        }

        /// <summary>
        /// Removes a component from the grid layout
        /// </summary>
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

        /// <summary>
        /// Updates the positions and sizes of all components in the grid
        /// </summary>
        public void UpdateComponentPositions()
        {
            foreach (var component in _components)
            {
                PositionComponent(component);
            }
        }

        /// <summary>
        /// Set the spacing between grid cells
        /// </summary>
        public void SetSpacing(int horizontal, int vertical)
        {
            HorizontalSpacing = horizontal;
            VerticalSpacing = vertical;

            // Update all component positions with the new spacing values
            UpdateComponentPositions();
        }

        /// <summary>
        /// Clears all components from the grid layout
        /// </summary>
        public void Clear()
        {
            _componentPositions.Clear();
            _components.Clear();
        }

        /// <summary>
        /// Gets all components managed by this grid layout
        /// </summary>
        public IReadOnlyCollection<BaseComponent> GetComponents()
        {
            return _components.AsReadOnly();
        }

        /// <summary>
        /// Gets the grid position information for a component
        /// </summary>
        public (int Column, int Row, int ColumnSpan, int RowSpan)? GetComponentPosition(string componentId)
        {
            if (_componentPositions.TryGetValue(componentId, out var position))
            {
                return position;
            }
            return null;
        }

        /// <summary>
        /// Updates the parent menu reference and recalculates component positions
        /// </summary>
        public void SetParentMenu(BaseMenu menu)
        {
            _parentMenu = menu ?? throw new ArgumentNullException(nameof(menu));
            UpdateOrigin();
        }
    }
}