using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Layout
{
    public class GridLayout
    {
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }

        public Vector2 GetCellPosition(int column, int row);

        public void AddComponent(BaseComponent component, int column, int row, int columnSpan = 1, int rowSpan = 1);

        public void RemoveComponent(string componentId);
    }
}