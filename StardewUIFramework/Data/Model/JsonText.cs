using System.Globalization;
using Newtonsoft.Json.Linq;

namespace UIFramework.Data.Model
{
    /// <summary>The text of a JSON token as data reads it: scalar values in the invariant culture (<c>0.5</c>, <c>-1</c> on every system).</summary>
    internal static class JsonText
    {
        /// <summary>The text of <paramref name="token"/>; a value token is formatted with the invariant culture.</summary>
        internal static string Of(JToken token)
        {
            return token is JValue value ? value.ToString(CultureInfo.InvariantCulture) : token.ToString();
        }
    }
}
