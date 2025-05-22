using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Components.Base;

namespace UIFramework.Events
{
    public class UIEventArgs : EventArgs
    {
        public BaseComponent Source { get; set; }
        public DateTime Timestamp { get; set; }

        public UIEventArgs(BaseComponent source)
        {
            Source = source;
            Timestamp = DateTime.Now;
        }
    }
}