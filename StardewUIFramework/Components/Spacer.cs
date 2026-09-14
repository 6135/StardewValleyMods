using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Components
{
    /// <summary>Empty space; with <see cref="Line"/> it draws a thin divider across its bounds.</summary>
    internal sealed class Spacer : UIElement, IUISpacer
    {
        private const int LineThickness = 4;

        internal Spacer(string id, int width, int height) : base(id)
        {
            Width = width;
            Height = height;
        }

        public bool Line { get; set; }

        protected override bool IsHitTestVisible => false;

        protected override Vector2 MeasureCore(Vector2 available) => new(0, Line ? LineThickness : 0);

        protected override void DrawCore(SpriteBatch b)
        {
            if (!Line || Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            bool vertical = Bounds.Height > Bounds.Width;
            Rectangle line = vertical
                ? new Rectangle(Bounds.Center.X - (LineThickness / 2), Bounds.Y, LineThickness, Bounds.Height)
                : new Rectangle(Bounds.X, Bounds.Center.Y - (LineThickness / 2), Bounds.Width, LineThickness);
            DrawHelper.Fill(b, line, Style.TextColor * 0.35f);
        }
    }
}
