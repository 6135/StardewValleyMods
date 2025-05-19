using Microsoft.Xna.Framework.Graphics;
using UIFramework.Components.Base;

namespace UIFramework.Components
{
    public class Dropdown : BaseClickableComponent
    {
        public string[] Options { get; set; }
        public int SelectedIndex { get; set; }

        public event Action<int> SelectionChanged;

        public bool Expanded { get; protected set; }
        public int MaxVisibleItems { get; set; } = 5;

        public override void Draw(SpriteBatch b);

        public override void OnClick(int x, int y);
    }
}