using System;
using System.Collections.Generic;
using UIFramework.Components;

namespace UIFramework.Core
{
    /// <summary>
    /// The bindings a consumer created between its signals / computeds and its elements, keyed by element and binding
    /// kind (one text, one visible, one enabled and one value binding per element at most). A binding is dropped by
    /// <c>Unbind</c>, when the element leaves its menu tree and when its menu is destroyed.
    /// </summary>
    internal sealed class SignalBindings
    {
        internal const string TextKind = "text";
        internal const string VisibleKind = "visible";
        internal const string EnabledKind = "enabled";
        internal const string ValueKind = "value";

        private readonly Dictionary<UIElement, Dictionary<string, SignalBinding>> table = new();

        /// <summary>Install <paramref name="binding"/>, replacing a previous binding of the same kind on the element.</summary>
        internal void Add(UIElement element, string kind, SignalBinding binding)
        {
            if (!table.TryGetValue(element, out var kinds))
            {
                table[element] = kinds = new Dictionary<string, SignalBinding>();
            }

            if (kinds.Remove(kind, out SignalBinding? previous))
            {
                previous.Dispose();
            }

            kinds[kind] = binding;
            element.BindingOwner = this; // so detaching the element drops this table's bindings, whoever owns the menu
        }

        /// <summary>Dispose every binding on <paramref name="element"/>.</summary>
        internal void Drop(UIElement element)
        {
            if (element.BindingOwner == this)
            {
                element.BindingOwner = null;
            }

            if (!table.Remove(element, out var kinds))
            {
                return;
            }

            foreach (SignalBinding binding in kinds.Values)
            {
                binding.Dispose();
            }
        }

        /// <summary>Dispose the bindings of every element that belongs to <paramref name="menu"/>.</summary>
        internal void DropMenu(UIMenu menu)
        {
            foreach (UIElement element in new List<UIElement>(table.Keys))
            {
                if (element.OwnerMenu == menu)
                {
                    Drop(element);
                }
            }
        }
    }

    /// <summary>A live link between a reactive value and an element; disposing restores the element.</summary>
    internal abstract class SignalBinding
    {
        internal abstract void Dispose();
    }

    /// <summary>
    /// Feeds a label from a reactive value. The label's text delegate returns a cached string, so the framework's
    /// per-frame evaluation is free; the cache is refreshed (and the layout invalidated only if the text really
    /// changed) when the source notifies with a new version.
    /// </summary>
    internal sealed class TextBinding : SignalBinding
    {
        private readonly Label label;
        private readonly Reactive source;
        private readonly Action refresh;
        private string cached = string.Empty;
        private int lastVersion = -1;

        internal TextBinding(Label label, Reactive source)
        {
            this.label = label;
            this.source = source;
            refresh = Refresh;
            label.TextFunc = () => cached;
            source.SubscribeInternal(refresh);
            Refresh();
        }

        private void Refresh()
        {
            if (source.Version == lastVersion)
            {
                return;
            }

            lastVersion = source.Version;
            string text = source.Current.Text;
            if (string.Equals(text, cached, StringComparison.Ordinal))
            {
                return;
            }

            cached = text;
            label.InvalidateLayout();
        }

        internal override void Dispose()
        {
            source.UnsubscribeInternal(refresh);
            string frozen = cached;
            label.TextFunc = () => frozen;
        }
    }

    /// <summary>Drives a boolean element property (<c>Visible</c> / <c>Enabled</c>) from a reactive flag.</summary>
    internal sealed class FlagBinding : SignalBinding
    {
        private readonly Reactive source;
        private readonly Action refresh;

        internal FlagBinding(UIElement element, Reactive source, Action<UIElement, bool> apply)
        {
            this.source = source;
            refresh = () => apply(element, this.source.Current.Flag);
            source.SubscribeInternal(refresh);
            refresh();
        }

        internal override void Dispose() => source.UnsubscribeInternal(refresh);
    }

    /// <summary>
    /// Two-way link between an input's value delegates and a signal: the input reads the signal and writes it (then
    /// the setter the input was created with, so the consumer's own state stays in sync). Disposing restores the
    /// original delegates.
    /// </summary>
    internal sealed class ValueBinding<T> : SignalBinding
    {
        private readonly Action<Func<T>?, Action<T>?> rebind;
        private readonly Func<T>? originalGetter;
        private readonly Action<T>? originalSetter;

        internal ValueBinding(Func<T>? originalGetter, Action<T>? originalSetter, Action<Func<T>?, Action<T>?> rebind, Func<T> read, Action<T> write)
        {
            this.originalGetter = originalGetter;
            this.originalSetter = originalSetter;
            this.rebind = rebind;
            Action<T>? chained = originalSetter;
            rebind(read, v =>
            {
                write(v);
                chained?.Invoke(v);
            });
        }

        internal override void Dispose() => rebind(originalGetter, originalSetter);
    }
}
