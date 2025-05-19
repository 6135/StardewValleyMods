using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Components.Base
{
    public abstract class BaseClickableComponent : BaseComponent
    {
        public event Action<ClickEventArgs> Clicked;

        public virtual void OnClick(int x, int y);

        public virtual void OnRightClick(int x, int y);

        public virtual void OnHover(int x, int y);
    }
}