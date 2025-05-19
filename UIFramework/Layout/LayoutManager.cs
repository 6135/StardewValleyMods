using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIFramework.Menus;

namespace UIFramework.Layout
{
    public class LayoutManager
    {
        private Dictionary<string, object> Layouts { get; set; } = new Dictionary<string, object>();

        public void RegisterLayout(string id, object layout);

        public T GetLayout<T>(string id) where T : class;

        public void RemoveLayout(string id);

        public void ClearLayouts();

        public void ApplyLayout(BaseMenu menu, string layoutId);

        public void UpdateLayout(string layoutId);
    }
}