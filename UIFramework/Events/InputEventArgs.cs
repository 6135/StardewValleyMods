using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Events
{
    public class InputEventArgs : UIEventArgs
    {
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public InputEventArgs(BaseComponent source, string oldValue, string newValue)
            : base(source)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}