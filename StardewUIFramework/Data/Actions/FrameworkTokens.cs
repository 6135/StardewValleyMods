using System;
using StardewValley;
using StardewValley.TokenizableStrings;
using UIFramework.Data.State;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The framework's tokenizable-string token <c>[6135.UIFramework_State &lt;key&gt;]</c> (dialogue, mail, item and
    /// event text): the text of a state value, e.g. <c>[6135.UIFramework_State player[Your.Pack].nickname]</c>. Unqualified
    /// keys work when the text is parsed while a data UI action runs.
    /// </summary>
    internal static class FrameworkTokens
    {
        internal const string StateToken = "6135.UIFramework_State";

        /// <summary>Register the token (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            TokenParser.RegisterParser(StateToken, (string[] query, out string replacement, Random random, Farmer player) =>
            {
                replacement = string.Empty;
                if (!ArgUtility.TryGet(query, 1, out string key, out string error, allowBlank: false, "string key"))
                {
                    return TokenParser.LogTokenError(query, error, out replacement);
                }

                DataScope? scope = DataActionRunner.Ambient;
                if (!StateAddress.TryParse(key, scope, allowBare: scope != null, out StateAddress address, out error))
                {
                    return TokenParser.LogTokenError(query, error, out replacement);
                }

                replacement = data.State.Read(address).AsString();
                return true;
            });
        }
    }
}
