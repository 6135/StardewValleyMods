using System;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>
    /// Cross-mod access rules for the v2 slot / decoration features (architecture.md §16.1). The framework cannot
    /// attribute a property setter to a caller, so <see cref="IUIElement.Sealed"/> is enforced where the caller is
    /// known: element lookup on behalf of another mod, and tree edits made through another mod's API instance.
    /// <para>
    /// Rules, walking from the element up to the root: the menu owner is never restricted; a contributor may reach
    /// anything under the container handed to it (even below a sealed ancestor); anyone else is stopped by the first
    /// sealed element on the path, and may only edit the tree at all when registered as a decorator of the menu.
    /// </para>
    /// </summary>
    internal static class Sealing
    {
        /// <summary>The contributor a subtree is attributed to (nearest contributor container), or null for owner-built elements.</summary>
        internal static ConsumerContext? ContributorOf(UIElement element)
        {
            for (UIElement? e = element; e != null; e = e.ParentElement)
            {
                if (e.Contributor != null)
                {
                    return e.Contributor;
                }
            }
            return null;
        }

        /// <summary>Whether <paramref name="consumer"/> may see <paramref name="element"/> (owner, its own contribution, or no sealed element on the path).</summary>
        internal static bool IsReachable(UIElement element, ConsumerContext consumer)
        {
            if (IsOwner(element, consumer))
            {
                return true;
            }

            for (UIElement? e = element; e != null; e = e.ParentElement)
            {
                if (IsContributor(e, consumer))
                {
                    return true;
                }

                if (e.Sealed)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Find the first element with <paramref name="id"/> under <paramref name="root"/> that <paramref name="consumer"/> may see.</summary>
        internal static UIElement? FindReachable(UIElement root, string id, ConsumerContext consumer)
        {
            foreach (UIElement e in root.SelfAndDescendants())
            {
                if (e.Id == id && IsReachable(e, consumer))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>
        /// Throw unless <paramref name="consumer"/> may add to / remove <paramref name="element"/>: the owner always may,
        /// a contributor may inside its own container, a decorator (<paramref name="isDecorator"/>) may outside sealed
        /// subtrees, nobody else may. Detached elements are unrestricted.
        /// </summary>
        internal static void RequireWriteAccess(UIElement element, ConsumerContext consumer, bool isDecorator)
        {
            if (element.OwnerMenu == null || IsOwner(element, consumer))
            {
                return;
            }

            bool sealedOnPath = false;
            for (UIElement? e = element; e != null; e = e.ParentElement)
            {
                if (IsContributor(e, consumer))
                {
                    return;
                }

                sealedOnPath |= e.Sealed;
            }

            if (sealedOnPath)
            {
                throw new InvalidOperationException($"'{element.Id}' is inside a sealed subtree of {element.OwnerMenu.Consumer.ModId}'s menu '{element.OwnerMenu.Id}'.");
            }

            if (!isDecorator)
            {
                throw new InvalidOperationException($"'{element.Id}' belongs to another mod's menu ({element.OwnerMenu.Consumer.ModId}); contribute to one of its slots or register OnScreenBuilt for it.");
            }
        }

        private static bool IsOwner(UIElement element, ConsumerContext consumer)
        {
            return element.OwnerMenu != null && element.OwnerMenu.Consumer.ModId == consumer.ModId;
        }

        private static bool IsContributor(UIElement element, ConsumerContext consumer)
        {
            return element.Contributor != null && element.Contributor.ModId == consumer.ModId;
        }
    }
}
