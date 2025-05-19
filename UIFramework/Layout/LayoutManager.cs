using System;
using System.Collections.Generic;
using UIFramework.Menus;

namespace UIFramework.Layout
{
    /// <summary>
    /// Manages different layout instances for the UI framework, providing a central
    /// registry for layouts that can be referenced and applied to menus.
    /// </summary>
    public class LayoutManager
    {
        private readonly Dictionary<string, object> _layouts = new Dictionary<string, object>();

        /// <summary>
        /// Registers a layout with the given ID.
        /// </summary>
        /// <param name="id">The unique identifier for the layout.</param>
        /// <param name="layout">The layout object to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when layout is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a layout with the same ID already exists.</exception>
        public void RegisterLayout(string id, object layout)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException(nameof(id), "Layout ID cannot be null or empty.");

            if (layout == null)
                throw new ArgumentNullException(nameof(layout), "Layout cannot be null.");

            if (_layouts.ContainsKey(id))
                throw new ArgumentException($"A layout with ID '{id}' already exists.", nameof(id));

            _layouts[id] = layout;
        }

        /// <summary>
        /// Retrieves a layout of the specified type with the given ID.
        /// </summary>
        /// <typeparam name="T">The type of layout to retrieve.</typeparam>
        /// <param name="id">The unique identifier of the layout.</param>
        /// <returns>The layout instance if found and of the correct type; otherwise, null.</returns>
        public T GetLayout<T>(string id) where T : class
        {
            if (string.IsNullOrEmpty(id) || !_layouts.TryGetValue(id, out var layout))
                return null;

            return layout as T;
        }

        /// <summary>
        /// Removes a layout with the specified ID.
        /// </summary>
        /// <param name="id">The unique identifier of the layout to remove.</param>
        /// <returns>True if the layout was removed; otherwise, false.</returns>
        public bool RemoveLayout(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            return _layouts.Remove(id);
        }

        /// <summary>
        /// Clears all registered layouts.
        /// </summary>
        public void ClearLayouts()
        {
            _layouts.Clear();
        }

        /// <summary>
        /// Applies a registered layout to a menu, positioning its components according to the layout.
        /// </summary>
        /// <param name="menu">The menu to apply the layout to.</param>
        /// <param name="layoutId">The ID of the layout to apply.</param>
        /// <returns>True if the layout was successfully applied; otherwise, false.</returns>
        public bool ApplyLayout(BaseMenu menu, string layoutId)
        {
            if (menu == null)
                throw new ArgumentNullException(nameof(menu), "Menu cannot be null.");

            if (string.IsNullOrEmpty(layoutId) || !_layouts.TryGetValue(layoutId, out var layout))
                return false;

            if (layout is GridLayout gridLayout)
            {
                ApplyGridLayout(menu, gridLayout);
                return true;
            }
            else if (layout is RelativeLayout relativeLayout)
            {
                ApplyRelativeLayout(menu, relativeLayout);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates a registered layout, recalculating component positions if necessary.
        /// </summary>
        /// <param name="layoutId">The ID of the layout to update.</param>
        /// <returns>True if the layout was successfully updated; otherwise, false.</returns>
        public bool UpdateLayout(string layoutId)
        {
            if (string.IsNullOrEmpty(layoutId) || !_layouts.TryGetValue(layoutId, out var layout))
                return false;

            if (layout is RelativeLayout relativeLayout)
            {
                relativeLayout.UpdateLayout();
                return true;
            }

            // GridLayout doesn't need explicit updating as it recalculates on Add/Remove
            return layout is GridLayout;
        }

        private void ApplyGridLayout(BaseMenu menu, GridLayout gridLayout)
        {
            // Apply each component in the grid layout to the menu
            foreach (var component in gridLayout.GetComponents())
            {
                // Add the component to the menu if it's not already there
                if (menu.GetComponent(component.Id) == null)
                {
                    menu.AddComponent(component);
                }
            }
        }

        private void ApplyRelativeLayout(BaseMenu menu, RelativeLayout relativeLayout)
        {
            // Apply each component in the relative layout to the menu
            foreach (var component in relativeLayout.GetComponents())
            {
                // Add the component to the menu if it's not already there
                if (menu.GetComponent(component.Id) == null)
                {
                    menu.AddComponent(component);
                }
            }
        }

        /// <summary>
        /// Creates a new grid layout, registers it with the given ID, and returns it.
        /// </summary>
        /// <param name="id">The unique identifier for the layout.</param>
        /// <param name="columns">The number of columns in the grid.</param>
        /// <param name="rows">The number of rows in the grid.</param>
        /// <param name="cellWidth">The width of each cell in the grid.</param>
        /// <param name="cellHeight">The height of each cell in the grid.</param>
        /// <returns>The created grid layout.</returns>
        public GridLayout CreateGridLayout(string id, int columns, int rows, int cellWidth, int cellHeight)
        {
            var gridLayout = new GridLayout(columns, rows, cellWidth, cellHeight);
            RegisterLayout(id, gridLayout);
            return gridLayout;
        }

        /// <summary>
        /// Creates a new relative layout, registers it with the given ID, and returns it.
        /// </summary>
        /// <param name="id">The unique identifier for the layout.</param>
        /// <returns>The created relative layout.</returns>
        public RelativeLayout CreateRelativeLayout(string id)
        {
            var relativeLayout = new RelativeLayout();
            RegisterLayout(id, relativeLayout);
            return relativeLayout;
        }
    }
}