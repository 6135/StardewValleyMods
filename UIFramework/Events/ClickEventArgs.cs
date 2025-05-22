using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Events
{
    public class ClickEventArgs : UIEventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public MouseButton Button { get; set; }
        public bool IsDoubleClick { get; set; }

        public enum MouseButton
        { Left, Right, Middle }

        public ClickEventArgs(BaseComponent source, int x, int y, MouseButton button, bool isDoubleClick = false)
            : base(source)
        {
            X = x;
            Y = y;
            Button = button;
            IsDoubleClick = isDoubleClick;
        }
    }
}