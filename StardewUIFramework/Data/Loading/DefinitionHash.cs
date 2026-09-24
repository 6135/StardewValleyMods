using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace UIFramework.Data.Loading
{
    /// <summary>
    /// Content hash of a definition (its canonical JSON), so a reload only rebuilds the entries that changed. Content
    /// Patcher invalidates its assets at every day start when a patch has conditions; unchanged entries are skipped.
    /// </summary>
    internal static class DefinitionHash
    {
        /// <summary>Hex SHA-256 of the definition's JSON (plus any extra inputs, e.g. the hash of the sprites it may use).</summary>
        internal static string Of(object? definition, params string[] extra)
        {
            string json = JsonConvert.SerializeObject(definition, DataAssetReader.Settings);
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json + "|" + string.Join("|", extra)));
            return Convert.ToHexString(bytes, 0, 16);
        }
    }
}
