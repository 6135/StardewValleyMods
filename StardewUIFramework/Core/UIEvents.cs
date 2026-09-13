using Microsoft.Xna.Framework.Input;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>Base event data handed to consumer callbacks.</summary>
    internal abstract class UIEvent : IUIEvent
    {
        public UIEventKind Kind { get; }
        public UIElement? Target { get; }
        public IUIElement Element => Target!;
        public string ElementId => Target?.Id ?? string.Empty;
        public bool Handled { get; set; }

        protected UIEvent(UIEventKind kind, UIElement? target)
        {
            Kind = kind;
            Target = target;
        }
    }

    internal sealed class UIClickEvent : UIEvent, IUIClickEvent
    {
        public int X { get; }
        public int Y { get; }
        public UIMouseButton Button { get; }

        public UIClickEvent(UIElement? target, int x, int y, UIMouseButton button)
            : base(button == UIMouseButton.Left ? UIEventKind.Click : UIEventKind.RightClick, target)
        {
            X = x;
            Y = y;
            Button = button;
        }
    }

    internal sealed class UIValueEvent : UIEvent, IUIValueEvent
    {
        public string OldValue { get; init; } = string.Empty;
        public string NewValue { get; init; } = string.Empty;
        public double OldNumber { get; init; }
        public double NewNumber { get; init; }
        public bool OldBool { get; init; }
        public bool NewBool { get; init; }
        public int OldIndex { get; init; } = -1;
        public int NewIndex { get; init; } = -1;

        public UIValueEvent(UIElement target) : base(UIEventKind.ValueChanged, target) { }

        public static UIValueEvent Text(UIElement target, string oldValue, string newValue) => new(target)
        {
            OldValue = oldValue,
            NewValue = newValue
        };

        public static UIValueEvent Number(UIElement target, double oldValue, double newValue) => new(target)
        {
            OldNumber = oldValue,
            NewNumber = newValue,
            OldValue = oldValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NewValue = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        public static UIValueEvent Bool(UIElement target, bool oldValue, bool newValue) => new(target)
        {
            OldBool = oldValue,
            NewBool = newValue,
            OldValue = oldValue.ToString(),
            NewValue = newValue.ToString()
        };

        public static UIValueEvent Index(UIElement target, int oldIndex, int newIndex, string oldValue, string newValue) => new(target)
        {
            OldIndex = oldIndex,
            NewIndex = newIndex,
            OldNumber = oldIndex,
            NewNumber = newIndex,
            OldValue = oldValue,
            NewValue = newValue
        };
    }

    internal sealed class UIKeyEvent : UIEvent, IUIKeyEvent
    {
        public Keys Key { get; }
        public bool Shift { get; }
        public bool Ctrl { get; }
        public bool Alt { get; }

        public UIKeyEvent(UIElement? target, Keys key, bool shift, bool ctrl, bool alt) : base(UIEventKind.Key, target)
        {
            Key = key;
            Shift = shift;
            Ctrl = ctrl;
            Alt = alt;
        }
    }
}
