using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>Kind of a <see cref="TooltipBlock"/>.</summary>
    internal enum TooltipBlockKind
    {
        Title,
        Line,
        Icon,
        Item,
        Divider,
        Money
    }

    /// <summary>One block of a <see cref="RichTooltip"/>; only the members matching <see cref="Kind"/> are set.</summary>
    internal sealed class TooltipBlock
    {
        internal TooltipBlock(TooltipBlockKind kind)
        {
            Kind = kind;
        }

        internal TooltipBlockKind Kind { get; }

        /// <summary>Title / line text (rich markup).</summary>
        internal Func<string>? Text { get; init; }

        /// <summary>Line color (null = theme text color).</summary>
        internal Color? Color { get; init; }

        internal Texture2D? Texture { get; init; }
        internal Rectangle? Source { get; init; }
        internal float Scale { get; init; } = 1f;

        /// <summary>Qualified item id for <see cref="TooltipBlockKind.Item"/>.</summary>
        internal string ItemId { get; init; } = string.Empty;

        internal Func<int>? Amount { get; init; }
    }

    /// <summary>
    /// The model behind <see cref="IUITooltip"/>: an ordered list of blocks plus a maximum width. Drawing lives in
    /// <see cref="Rendering.TooltipRenderer"/>; delegates are evaluated there through the owning element's guard.
    /// </summary>
    internal sealed class RichTooltip : IUITooltip
    {
        private readonly List<TooltipBlock> blocks = new();

        internal IReadOnlyList<TooltipBlock> Blocks => blocks;

        /// <summary>Wrap width in UI pixels (0 = only the viewport limits it).</summary>
        internal int MaxWidthPx { get; private set; }

        public IUITooltip Title(Func<string> title) => Add(new TooltipBlock(TooltipBlockKind.Title) { Text = title });

        public IUITooltip Line(Func<string> text) => Add(new TooltipBlock(TooltipBlockKind.Line) { Text = text });

        public IUITooltip Line(Func<string> text, Color color) => Add(new TooltipBlock(TooltipBlockKind.Line) { Text = text, Color = color });

        public IUITooltip Icon(Texture2D texture, Rectangle? source, float scale)
        {
            if (texture == null)
            {
                return this;
            }

            return Add(new TooltipBlock(TooltipBlockKind.Icon) { Texture = texture, Source = source, Scale = Math.Max(0.05f, scale) });
        }

        public IUITooltip Item(string qualifiedItemId) => Add(new TooltipBlock(TooltipBlockKind.Item) { ItemId = qualifiedItemId ?? string.Empty });

        public IUITooltip Divider() => Add(new TooltipBlock(TooltipBlockKind.Divider));

        public IUITooltip Money(Func<int> amount) => Add(new TooltipBlock(TooltipBlockKind.Money) { Amount = amount });

        public IUITooltip MaxWidth(int px)
        {
            MaxWidthPx = Math.Max(0, px);
            return this;
        }

        public IUITooltip Clear()
        {
            blocks.Clear();
            return this;
        }

        private RichTooltip Add(TooltipBlock block)
        {
            blocks.Add(block);
            return this;
        }
    }
}
