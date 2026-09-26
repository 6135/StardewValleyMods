namespace UIFramework
{
    /// <summary>
    /// The subset of the Stardew Access API (<c>shoaib.stardewaccess</c>) used by the framework, copied from
    /// <c>stardew-access/PublicApi/IStardewAccessApi.cs</c> as of Stardew Access 1.6.2.
    /// </summary>
    public interface IStardewAccessApi
    {
        /// <summary>Speaks the text via the loaded screen reader (if any).</summary>
        /// <param name="text">The text to be narrated.</param>
        /// <param name="interrupt">Whether to skip the currently speaking text or not.</param>
        /// <returns>true if the text was spoken otherwise false.</returns>
        bool Say(string text, bool interrupt);
    }
}
