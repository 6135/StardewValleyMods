using System;
using System.Text;
using UIFramework.Core;

namespace UIFramework.Rendering
{
    /// <summary>
    /// Pseudo-localization (<see cref="ModConfig.PseudoLocalize"/>): every string that reaches a draw / measure
    /// through the framework's text path is accented and padded so layout overflow shows up before a real
    /// translation exists, e.g. <c>"Calculate"</c> → <c>"[Çálçúláté~~~]"</c>. Text the player types, ids and
    /// markup tags are never transformed.
    /// </summary>
    internal static class Pseudo
    {
        /// <summary>Extra length added as <c>~</c> padding, as a fraction of the visible text length.</summary>
        private const double PadRatio = 0.3;

        /// <summary>Whether the mode is on.</summary>
        internal static bool Enabled => UIServices.Config.PseudoLocalize;

        /// <summary>Accent, pad and bracket <paramref name="text"/> when the mode is on; otherwise return it unchanged.</summary>
        internal static string Transform(string? text)
        {
            if (!Enabled || string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            return "[" + Accent(text) + Padding(text.Length) + "]";
        }

        /// <summary>The <c>~</c> padding for a visible text of <paramref name="length"/> characters.</summary>
        internal static string Padding(int length)
        {
            int pad = (int)Math.Ceiling(Math.Max(0, length) * PadRatio);
            return pad > 0 ? new string('~', pad) : string.Empty;
        }

        /// <summary>Replace vowels and a few consonants by accented look-alikes (both cases).</summary>
        internal static string Accent(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(AccentChar(c));
            }

            return sb.ToString();
        }

        private static char AccentChar(char c) => c switch
        {
            'a' => 'á',
            'e' => 'é',
            'i' => 'í',
            'o' => 'ó',
            'u' => 'ú',
            'c' => 'ç',
            'n' => 'ñ',
            'y' => 'ý',
            'A' => 'Á',
            'E' => 'É',
            'I' => 'Í',
            'O' => 'Ó',
            'U' => 'Ú',
            'C' => 'Ç',
            'N' => 'Ñ',
            'Y' => 'Ý',
            _ => c
        };
    }
}
