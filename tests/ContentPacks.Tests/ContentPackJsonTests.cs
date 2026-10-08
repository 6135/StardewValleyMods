using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ContentPacks.Tests;

/// <summary>Checks that every content pack's JSON parses the way SMAPI reads it.</summary>
public class ContentPackJsonTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    public static IEnumerable<object[]> JsonFiles() =>
        Directory.GetDirectories(RepoRoot)
            .Where(dir => Path.GetFileName(dir).StartsWith("[CP]", StringComparison.Ordinal))
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Select(path => new object[] { Path.GetRelativePath(RepoRoot, path) });

    [Theory]
    [MemberData(nameof(JsonFiles))]
    public void JsonFile_Parses(string relativePath)
    {
        JToken token = Parse(relativePath);

        if (Path.GetFileName(relativePath) == "manifest.json")
        {
            Assert.False(string.IsNullOrWhiteSpace(token.Value<string>("UniqueID")), "missing UniqueID");
            Assert.False(string.IsNullOrWhiteSpace(token.Value<string>("Version")), "missing Version");
            Assert.False(string.IsNullOrWhiteSpace(token["ContentPackFor"]?.Value<string>("UniqueID")), "missing ContentPackFor.UniqueID");
        }
    }

    private static JToken Parse(string relativePath)
    {
        using var reader = new JsonTextReader(new StreamReader(Path.Combine(RepoRoot, relativePath)));
        return JToken.ReadFrom(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore });
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Stardew Mods.sln")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Could not find 'Stardew Mods.sln' above " + AppContext.BaseDirectory);
    }
}
