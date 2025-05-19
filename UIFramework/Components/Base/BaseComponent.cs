using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Components.Base
{
    public abstract class BaseComponent
    {
        public string Id { get; protected set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;
        public string Tooltip { get; set; }

        public abstract void Draw(SpriteBatch b);

        public abstract void Update(GameTime time);

        public virtual bool Contains(int x, int y);

        public virtual void OnResize(Rectangle oldBounds, Rectangle newBounds);
    }
}