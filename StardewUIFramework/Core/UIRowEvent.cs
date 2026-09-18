using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>A click on a data grid row: the click data plus the underlying row index.</summary>
    internal sealed class UIRowEvent : UIEvent, IUIRowEvent
    {
        public int X { get; }
        public int Y { get; }
        public UIMouseButton Button { get; }
        public int Row { get; }

        internal UIRowEvent(UIElement target, UIClickEvent click, int row)
            : base(click.Kind, target)
        {
            X = click.X;
            Y = click.Y;
            Button = click.Button;
            Row = row;
        }
    }
}
