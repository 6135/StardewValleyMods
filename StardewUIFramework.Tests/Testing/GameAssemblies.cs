using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace StardewUIFramework.Tests.Testing
{
    /// <summary>
    /// Resolves the game, SMAPI and MonoGame assemblies from the game folder when the test host asks for them.
    /// ModBuildConfig references them without copying (they are never shipped with a mod), so without this the
    /// first <c>Microsoft.Xna.Framework</c> type would fail to load. The game folder is the <c>GamePath</c>
    /// assembly metadata baked in by the test project (from <c>STARDEW_GAME_DIR</c>).
    /// </summary>
    internal static class GameAssemblies
    {
        private static readonly string[] SearchFolders = { string.Empty, "smapi-internal" };

        /// <summary>The game folder the test project was built against.</summary>
        internal static string GamePath { get; private set; } = string.Empty;

        [ModuleInitializer]
        internal static void Install()
        {
            foreach (AssemblyMetadataAttribute attribute in typeof(GameAssemblies).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (attribute.Key == "GamePath" && !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    GamePath = attribute.Value;
                }
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly? Resolve(object? sender, ResolveEventArgs args)
        {
            if (GamePath.Length == 0)
            {
                return null;
            }

            string name = new AssemblyName(args.Name).Name ?? string.Empty;
            foreach (string folder in SearchFolders)
            {
                string path = Path.Combine(GamePath, folder, name + ".dll");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }
            return null;
        }
    }
}
