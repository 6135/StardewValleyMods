using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Config
{
    public class MenuConfig
    {
        public string Title { get; set; } = "";
        public Vector2 Position { get; set; } = Vector2.Zero;
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public bool Draggable { get; set; } = false;
        public bool Resizable { get; set; } = false;
        public bool Modal { get; set; } = true;
        public bool ShowCloseButton { get; set; } = true;
        public SButton ToggleKey { get; set; } = SButton.None;
        public int TooltipDelay { get; set; } = -1; // -1 means use global setting
        public bool UseGameBackground { get; set; } = true;
        public Color? BackgroundColor { get; set; } = null;
        public string BackgroundTexture { get; set; } = "";
    }
}