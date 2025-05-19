using Microsoft.Xna.Framework.Input;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Components.Base
{
    public abstract class BaseInputComponent : BaseClickableComponent, IKeyboardSubscriber
    {
        public bool Selected { get; protected set; }

        public virtual string Value { get; set; }

        public virtual void OnKeyPressed(Keys key);

        public virtual void OnTextInput(char input);

        public virtual void Select();

        public virtual void Deselect();
    }
}