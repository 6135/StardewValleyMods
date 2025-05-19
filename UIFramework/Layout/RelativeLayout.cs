using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Layout
{
    public class RelativeLayout
    {
        public enum AnchorPoint
        { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }

        public void AddComponent(BaseComponent component, AnchorPoint anchor, Vector2 offset);

        public void AddComponent(BaseComponent component, BaseComponent relativeTo, AnchorPoint anchor, Vector2 offset);

        public void RemoveComponent(string componentId);
    }
}