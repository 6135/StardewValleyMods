using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Menus;
using System;
using UIFramework.main.ui.models;
using IDrawable = UIFramework.main.ui.models.IDrawable;

namespace UIFramework.main.ui.menus
{
    //class representing a menu. Menus are based on multiples of game tile sizes.
    internal abstract class Menu : IClickableMenu, IDrawable, IDisposable
    {
        public string Name;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Draw(SpriteBatch b)
        {
            throw new System.NotImplementedException();
        }

        public void GameWindowSizeChanged()
        {
            throw new System.NotImplementedException();
        }

        public void Reset()
        {
            throw new System.NotImplementedException();
        }

        public void Update()
        {
            throw new System.NotImplementedException();
        }
    }
}