using UIFramework.Api;
using UIFramework.Core;

namespace StardewUIFramework.Tests.Testing
{
    /// <summary>Deterministic text dump of a menu tree (type, id and bounds per line, indented by depth) for snapshot assertions.</summary>
    public static class TreeSnapshot
    {
        /// <summary>Lay the menu out and render its tree. Lines are separated by '\n'.</summary>
        public static string Render(IUIMenu menu)
        {
            UIMenu model = TestHost.Unwrap(menu);
            if (model.LayoutDirty || model.Root.LayoutDirty)
            {
                model.Relayout();
            }

            return TreeDump.Render(model.Root, includeState: false);
        }

        /// <summary>Render the subtree under one element without a layout pass.</summary>
        public static string Render(IUIElement element) => TreeDump.Render((UIElement)element, includeState: false);
    }
}
