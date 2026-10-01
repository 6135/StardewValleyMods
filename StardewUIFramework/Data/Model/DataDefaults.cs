using UIFramework.Api;

namespace UIFramework.Data.Model
{
    /// <summary>
    /// The data format's defaults: the value a menu option or element member has when a definition leaves it out.
    /// One nested class per element type (named like <see cref="ElementTypes"/>) plus <see cref="Menu"/>, one constant per
    /// field. Read by <c>DataBuilder</c> (construction arguments and reset values), by <c>TreeModelReader</c> (omitting
    /// defaults on export) and by the designer's metadata generator, so the three never disagree.
    /// </summary>
    internal static class DataDefaults
    {
        /// <summary>Menu options and the root stack's members.</summary>
        internal static class Menu
        {
            internal const bool ShowCloseButton = true;
            internal const bool Modal = true;
            internal const bool DimBackground = true;
            internal const UIAnchor Anchor = UIAnchor.Center;
            internal const int X = 0;
            internal const int Y = 0;
            internal const bool DrawBox = true;
            internal const int Padding = 0;
            internal const bool CloseOnEscape = true;
            internal const bool PlayerLayout = true;
            internal const bool Resizable = false;
            internal const bool Horizontal = false;
            internal const int Spacing = 8;
            internal const UIAlign Alignment = UIAlign.Start;
        }

        internal static class Stack
        {
            internal const bool Horizontal = false;
            internal const int Spacing = 8;
        }

        internal static class Repeat
        {
            internal const bool Horizontal = false;
            internal const int Spacing = 8;
        }

        internal static class Outlet
        {
            internal const bool Horizontal = false;
            internal const int Spacing = 8;
        }

        internal static class Grid
        {
            internal const string Columns = "*";
            internal const string Rows = "auto";
        }

        internal static class Panel
        {
            internal const bool DrawBox = true;
            internal const int Padding = 16;
        }

        internal static class ScrollView
        {
            internal const int ViewportHeight = 300;
        }

        internal static class Spacer
        {
            internal const int Width = 0;
            internal const int Height = 0;
        }

        internal static class Image
        {
            internal const float Scale = 4f;
        }

        internal static class ItemImage
        {
            internal const int Quality = 0;
            internal const int Count = 1;
            internal const float Scale = 4f;
        }

        internal static class NumberInput
        {
            internal const double Min = 0;
            internal const double Max = 999999;
            internal const double Step = 1;
            internal const bool Clamp = true;
        }

        internal static class Slider
        {
            internal const double Min = 0;
            internal const double Max = 100;
        }

        internal static class List
        {
            internal const int RowHeight = 48;
            internal const int VisibleRows = 6;
        }

        internal static class DataGrid
        {
            internal const int RowHeight = 48;
            internal const int VisibleRows = 6;
            internal const bool SortDescending = false;
        }
    }
}
