using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// An extension slot (architecture.md §16.1): a stack whose children are one container per contributing mod,
    /// rebuilt by <see cref="Hosting.ExtensionRegistry"/> every time the owning menu opens. Layout hints
    /// (<see cref="Stack.Horizontal"/>, <see cref="Stack.Wrap"/>, <see cref="MaxHeight"/>) and the visibility predicate
    /// are the owner's; each contribution's row copies Horizontal and Wrap when it is built;
    /// visibility itself is managed here: hidden while empty or while the predicate says so.
    /// </summary>
    internal sealed class Slot : Stack, IUISlot
    {
        private int? maxHeight;
        private string[] vetoed = Array.Empty<string>();

        internal Slot(string id) : base(id, horizontal: false, spacing: 8)
        {
        }

        public int? MaxHeight
        {
            get => maxHeight;
            set
            {
                maxHeight = value.HasValue ? Math.Max(0, value.Value) : null;
                InvalidateLayout();
            }
        }

        internal Func<bool>? VisiblePredicate { get; set; }

        Func<bool> IUISlot.VisiblePredicate { get => VisiblePredicate!; set => VisiblePredicate = value; }

        public int MaxContributions { get; set; }

        /// <summary>Mod ids whose contributions are skipped (never null).</summary>
        public string[] VetoedContributors
        {
            get => vetoed;
            set => vetoed = value ?? Array.Empty<string>();
        }

        /// <summary>Whether <paramref name="modId"/> was vetoed by the owner.</summary>
        internal bool IsVetoed(string modId) => Array.IndexOf(vetoed, modId) >= 0;

        /// <summary>Mod ids of the contributions currently built into this slot (in slot order).</summary>
        internal IEnumerable<string> ContributorIds()
        {
            foreach (UIElement child in Children)
            {
                if (child.Contributor != null)
                {
                    yield return child.Contributor.ModId;
                }
            }
        }

        /// <summary>Apply the managed visibility: contributions present and the predicate (guarded) not false.</summary>
        internal void ApplyVisibility()
        {
            Visible = ChildCount > 0 && Raise("VisiblePredicate", VisiblePredicate, true);
        }

        protected override Vector2 MeasureCore(Vector2 available)
        {
            if (maxHeight.HasValue)
            {
                available.Y = Math.Min(available.Y, maxHeight.Value);
            }

            Vector2 size = base.MeasureCore(available);
            if (maxHeight.HasValue)
            {
                size.Y = Math.Min(size.Y, maxHeight.Value);
            }

            return size;
        }

        internal override void Update(double elapsedMs)
        {
            ApplyVisibility();
            base.Update(elapsedMs);
        }
    }

    /// <summary>Snapshot of a slot's identity and hints for <see cref="IStardewUIApi.ListSlots"/>.</summary>
    internal sealed class SlotInfo : IUISlotInfo
    {
        internal SlotInfo(string ownerModId, string menuId, Slot slot)
        {
            OwnerModId = ownerModId;
            MenuId = menuId;
            SlotId = slot.Id;
            Horizontal = slot.Horizontal;
            MaxHeight = slot.MaxHeight;
        }

        public string OwnerModId { get; }
        public string MenuId { get; }
        public string SlotId { get; }
        public bool Horizontal { get; }
        public int? MaxHeight { get; }

        public override string ToString() => $"{OwnerModId}/{MenuId}/{SlotId}";
    }
}
