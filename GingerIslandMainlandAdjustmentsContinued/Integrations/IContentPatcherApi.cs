namespace GingerIslandMainlandAdjustments.Integrations;

/// <summary>The subset of Content Patcher's API this mod uses.</summary>
public interface IContentPatcherApi
{
    void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>?> getValue);
}
