using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using UIFramework.Components.Base;

namespace UIFramework.Layout
{
    public class RelativeLayout
    {
        public enum AnchorPoint
        { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

        // Public struct for returning layout information
        public struct ComponentLayoutData
        {
            public string ComponentId { get; set; }
            public string RelativeToId { get; set; }
            public AnchorPoint Anchor { get; set; }
            public Vector2 Offset { get; set; }
        }

        private class ComponentLayoutInfo
        {
            public BaseComponent Component { get; set; }
            public BaseComponent RelativeTo { get; set; }
            public AnchorPoint Anchor { get; set; }
            public Vector2 Offset { get; set; }
        }

        private readonly Dictionary<string, ComponentLayoutInfo> _componentLayouts;
        private readonly List<BaseComponent> _components;

        public RelativeLayout()
        {
            _componentLayouts = new Dictionary<string, ComponentLayoutInfo>();
            _components = new List<BaseComponent>();
        }

        public void AddComponent(BaseComponent component, AnchorPoint anchor, Vector2 offset)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            // Remove component if it already exists in the layout
            if (_componentLayouts.ContainsKey(component.Id))
            {
                RemoveComponent(component.Id);
            }

            // Add component to the layout
            var layoutInfo = new ComponentLayoutInfo
            {
                Component = component,
                RelativeTo = null,
                Anchor = anchor,
                Offset = offset
            };

            _componentLayouts[component.Id] = layoutInfo;
            _components.Add(component);

            // Apply the layout calculation immediately
            PositionComponent(layoutInfo);
        }

        public void AddComponent(BaseComponent component, BaseComponent relativeTo, AnchorPoint anchor, Vector2 offset)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));
            if (relativeTo == null)
                throw new ArgumentNullException(nameof(relativeTo));

            // Remove component if it already exists in the layout
            if (_componentLayouts.ContainsKey(component.Id))
            {
                RemoveComponent(component.Id);
            }

            // Add component to the layout
            var layoutInfo = new ComponentLayoutInfo
            {
                Component = component,
                RelativeTo = relativeTo,
                Anchor = anchor,
                Offset = offset
            };

            _componentLayouts[component.Id] = layoutInfo;
            _components.Add(component);

            // Apply the layout calculation immediately
            PositionComponent(layoutInfo);
        }

        public void RemoveComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return;

            if (_componentLayouts.TryGetValue(componentId, out var layoutInfo))
            {
                _componentLayouts.Remove(componentId);
                _components.Remove(layoutInfo.Component);
            }
        }

        public void UpdateLayout()
        {
            foreach (var layoutInfo in _componentLayouts.Values)
            {
                PositionComponent(layoutInfo);
            }
        }

        private void PositionComponent(ComponentLayoutInfo layoutInfo)
        {
            Vector2 basePosition;
            Vector2 relativeBounds = Vector2.Zero;

            // Determine base position
            if (layoutInfo.RelativeTo == null)
            {
                // Position relative to viewport
                basePosition = new Vector2(0, 0);
                relativeBounds = new Vector2(
                    StardewValley.Game1.viewport.Width,
                    StardewValley.Game1.viewport.Height
                );
            }
            else
            {
                // Position relative to another component
                basePosition = layoutInfo.RelativeTo.Position;
                relativeBounds = layoutInfo.RelativeTo.Size;
            }

            // Calculate position based on anchor point
            Vector2 position = CalculatePositionFromAnchor(
                layoutInfo.Anchor,
                basePosition,
                relativeBounds,
                layoutInfo.Component.Size
            );

            // Apply offset
            position += layoutInfo.Offset;

            // Set component position
            layoutInfo.Component.Position = position;
        }

        private Vector2 CalculatePositionFromAnchor(
            AnchorPoint anchor,
            Vector2 basePosition,
            Vector2 relativeBounds,
            Vector2 componentSize)
        {
            Vector2 position = basePosition;

            switch (anchor)
            {
                case AnchorPoint.TopLeft:
                    // No adjustment needed, this is the default
                    break;

                case AnchorPoint.Top:
                    position.X += (relativeBounds.X / 2) - (componentSize.X / 2);
                    break;

                case AnchorPoint.TopRight:
                    position.X += relativeBounds.X - componentSize.X;
                    break;

                case AnchorPoint.Left:
                    position.Y += (relativeBounds.Y / 2) - (componentSize.Y / 2);
                    break;

                case AnchorPoint.Center:
                    position.X += (relativeBounds.X / 2) - (componentSize.X / 2);
                    position.Y += (relativeBounds.Y / 2) - (componentSize.Y / 2);
                    break;

                case AnchorPoint.Right:
                    position.X += relativeBounds.X - componentSize.X;
                    position.Y += (relativeBounds.Y / 2) - (componentSize.Y / 2);
                    break;

                case AnchorPoint.BottomLeft:
                    position.Y += relativeBounds.Y - componentSize.Y;
                    break;

                case AnchorPoint.Bottom:
                    position.X += (relativeBounds.X / 2) - (componentSize.X / 2);
                    position.Y += relativeBounds.Y - componentSize.Y;
                    break;

                case AnchorPoint.BottomRight:
                    position.X += relativeBounds.X - componentSize.X;
                    position.Y += relativeBounds.Y - componentSize.Y;
                    break;
            }

            return position;
        }

        public void Clear()
        {
            _componentLayouts.Clear();
            _components.Clear();
        }

        public IReadOnlyCollection<BaseComponent> GetComponents()
        {
            return _components.AsReadOnly();
        }

        public void UpdateComponentOffset(string componentId, Vector2 newOffset)
        {
            if (_componentLayouts.TryGetValue(componentId, out var layoutInfo))
            {
                layoutInfo.Offset = newOffset;
                PositionComponent(layoutInfo);
            }
        }

        public void UpdateComponentAnchor(string componentId, AnchorPoint newAnchor)
        {
            if (_componentLayouts.TryGetValue(componentId, out var layoutInfo))
            {
                layoutInfo.Anchor = newAnchor;
                PositionComponent(layoutInfo);
            }
        }

        public void UpdateComponentRelativeTo(string componentId, BaseComponent newRelativeTo)
        {
            if (_componentLayouts.TryGetValue(componentId, out var layoutInfo))
            {
                layoutInfo.RelativeTo = newRelativeTo;
                PositionComponent(layoutInfo);
            }
        }

        /// <summary>
        /// Gets the layout information for a component.
        /// </summary>
        /// <param name="componentId">The ID of the component to get layout information for.</param>
        /// <returns>The layout information, or null if the component is not in the layout.</returns>
        public ComponentLayoutData? GetComponentLayout(string componentId)
        {
            if (_componentLayouts.TryGetValue(componentId, out var layoutInfo))
            {
                return new ComponentLayoutData
                {
                    ComponentId = layoutInfo.Component.Id,
                    RelativeToId = layoutInfo.RelativeTo?.Id,
                    Anchor = layoutInfo.Anchor,
                    Offset = layoutInfo.Offset
                };
            }
            return null;
        }
    }
}