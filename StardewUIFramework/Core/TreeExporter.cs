using UIFramework.Core.Export;

namespace UIFramework.Core
{
    /// <summary>
    /// Exports a menu tree for the inspector and <c>ui_export</c>: <see cref="TreeModelReader"/> walks the tree once
    /// into a format-neutral <see cref="TreeMenu"/>, then <see cref="CSharpEmitter"/> writes the C# builder code
    /// (<see cref="Export"/>) or <see cref="JsonEmitter"/> writes a <c>Mods/6135.UIFramework/Menus</c> entry
    /// (<see cref="ExportJson"/>). Delegates and textures that cannot be serialized become <c>// TODO</c> comments;
    /// the current value of text delegates is exported so the screen looks the same.
    /// </summary>
    internal static class TreeExporter
    {
        /// <summary>The C# builder code for <paramref name="menu"/>.</summary>
        internal static string Export(UIMenu menu) => CSharpEmitter.Emit(TreeModelReader.Read(menu));

        /// <summary>The data-format (JSON with comments) <c>Menus</c> entry for <paramref name="menu"/>.</summary>
        internal static string ExportJson(UIMenu menu) => JsonEmitter.Emit(TreeModelReader.Read(menu));

        /// <summary>A C# string literal for <paramref name="value"/>.</summary>
        internal static string Str(string? value) => CSharpEmitter.Str(value);
    }
}
