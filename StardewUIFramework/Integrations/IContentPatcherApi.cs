using StardewModdingAPI;

namespace UIFramework
{
    /// <summary>The part of Content Patcher's API the framework uses (the optional <c>6135.UIFramework/State</c> token).</summary>
    public interface IContentPatcherApi
    {
        /// <summary>Register an advanced token (an object with the <c>IsReady</c> / <c>GetValues</c> / <c>UpdateContext</c>... methods), named <c>&lt;mod id&gt;/&lt;name&gt;</c>.</summary>
        void RegisterToken(IManifest mod, string name, object token);
    }
}
