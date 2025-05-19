using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using System;
using UIFramework.Events;

namespace UIFramework.Components.Base
{
    public abstract class BaseClickableComponent : BaseComponent
    {
        public event Action<ClickEventArgs> Clicked;

        public event Action<ClickEventArgs> RightClicked;

        public event Action<ClickEventArgs> Hovered;

        public Color DefaultColor { get; set; } = Color.White;
        public Color HoverColor { get; set; } = Color.LightGray;
        public Color PressedColor { get; set; } = Color.Gray;
        public Color DisabledColor { get; set; } = new Color(120, 120, 120);

        protected bool _isPressed;
        protected string _hoverSound = "smallSelect";
        protected string _clickSound = "bigClick";

        protected BaseClickableComponent(string id, Vector2 position, Vector2 size)
            : base(id, position, size)
        {
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            bool wasHovered = _isHovered;
            _isHovered = Enabled && Visible && Contains(Game1.getMouseX(), Game1.getMouseY());

            if (_isHovered && !wasHovered)
            {
                OnHover(Game1.getMouseX(), Game1.getMouseY());
            }
        }

        public virtual void OnClick(int x, int y)
        {
            Clicked?.Invoke(new ClickEventArgs(this, x, y, ClickEventArgs.MouseButton.Left));
        }

        public virtual void OnRightClick(int x, int y)
        {
            RightClicked?.Invoke(new ClickEventArgs(this, x, y, ClickEventArgs.MouseButton.Right));
        }

        public virtual void OnHover(int x, int y)
        {
            Hovered?.Invoke(new ClickEventArgs(this, x, y, ClickEventArgs.MouseButton.Left));
        }

        public virtual void OnReleased()
        {
            _isPressed = false;
        }

        public void SetSounds(string hoverSound, string clickSound)
        {
            _hoverSound = hoverSound;
            _clickSound = clickSound;
        }
    }
}