using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIFramework.Config
{
    public class UIConfig
    {
        public int DefaultTooltipDelay { get; set; } = 500;
        public bool EnableHotkeySystem { get; set; } = true;
        public SButton DefaultToggleMenuKey { get; set; } = SButton.Tab;
        public float DefaultScale { get; set; } = 1.0f;
        public bool UsePixelSnapping { get; set; } = true;
        public Color DefaultTextColor { get; set; } = Game1.textColor;
        public string DefaultFontType { get; set; } = "Default";
    }
}