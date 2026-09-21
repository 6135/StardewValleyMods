using System.Globalization;
using System.Text;

namespace UIFramework.Core
{
    /// <summary>Renders a menu tree as indented text (one element per line) for the debug console and test snapshots.</summary>
    internal static class TreeDump
    {
        /// <summary>
        /// One line per element: type, id and absolute bounds, indented by depth. With <paramref name="includeState"/>
        /// each line also carries <c>visible=… enabled=…</c>.
        /// </summary>
        internal static string Render(UIElement root, bool includeState)
        {
            var sb = new StringBuilder();
            Append(sb, root, 0, includeState);
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>A single element line without indentation (used by <c>ui_find</c>).</summary>
        internal static string Line(UIElement element, bool includeState)
        {
            var sb = new StringBuilder();
            AppendLine(sb, element, includeState);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, UIElement element, int depth, bool includeState)
        {
            sb.Append(' ', depth * 2);
            AppendLine(sb, element, includeState);
            sb.Append('\n');
            if (element is UIContainer container)
            {
                foreach (UIElement child in container.Children)
                {
                    Append(sb, child, depth + 1, includeState);
                }
            }
        }

        private static void AppendLine(StringBuilder sb, UIElement element, bool includeState)
        {
            Microsoft.Xna.Framework.Rectangle b = element.Bounds;
            sb.Append(element.GetType().Name).Append(" '").Append(element.Id).Append("' [")
              .Append(b.X.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(b.Y.ToString(CultureInfo.InvariantCulture)).Append(' ')
              .Append(b.Width.ToString(CultureInfo.InvariantCulture)).Append('x')
              .Append(b.Height.ToString(CultureInfo.InvariantCulture)).Append(']');
            if (includeState)
            {
                sb.Append(" visible=").Append(element.Visible ? "true" : "false")
                  .Append(" enabled=").Append(element.Enabled ? "true" : "false");
            }
        }
    }
}
