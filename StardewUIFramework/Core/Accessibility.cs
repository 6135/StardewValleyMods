using System;
using System.Text;
using StardewModdingAPI;
using UIFramework.Components;

namespace UIFramework.Core
{
    /// <summary>
    /// Screen reader announcements (Stardew Access through <see cref="UIServices.Announcer"/>): the menu title when a
    /// menu opens, the focused element when focus moves, value changes on the focused element and the hovered element
    /// once the cursor rested on it for the tooltip delay. Every call is a no-op without a screen reader and never
    /// throws, so a missing or faulting API cannot affect the menu.
    /// </summary>
    internal static class Accessibility
    {
        private static bool faulted;

        /// <summary>True while a screen reader is available (skips description building otherwise).</summary>
        internal static bool Enabled => UIServices.Announcer != null && !faulted;

        /// <summary>Speak <paramref name="text"/> (ignored when empty or when no screen reader is present).</summary>
        internal static void Announce(string? text)
        {
            Action<string>? announcer = UIServices.Announcer;
            if (announcer == null || faulted || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                announcer(text);
            }
            catch (Exception ex)
            {
                faulted = true;
                UIServices.Log($"The screen reader integration threw an exception and has been disabled:\n{ex}", LogLevel.Warn);
            }
        }

        /// <summary>Speak the description of <paramref name="element"/> (the consumer's <c>AccessibleName</c> when set).</summary>
        internal static void AnnounceElement(UIElement? element)
        {
            if (element == null || !Enabled)
            {
                return;
            }

            Announce(Describe(element));
        }

        /// <summary>Speak the description of <paramref name="element"/> if it owns keyboard focus (value changed).</summary>
        internal static void AnnounceValue(UIElement element)
        {
            if (Enabled && element.IsFocused)
            {
                Announce(Describe(element));
            }
        }

        /// <summary>The spoken description: the consumer's <c>AccessibleName</c> when it returns text, else the element's own description.</summary>
        internal static string Describe(UIElement element)
        {
            if (element.AccessibleName != null)
            {
                string custom = element.Consumer.Invoke(element.Id, "AccessibleName", element.AccessibleName, string.Empty) ?? string.Empty;
                if (custom.Length > 0)
                {
                    return custom;
                }
            }

            return element.AccessibleDescription;
        }

        /// <summary>Join <paramref name="parts"/> that are not empty with ", " after the <paramref name="type"/> prefix, e.g. "Button: OK".</summary>
        internal static string Compose(string type, params string?[] parts)
        {
            var sb = new StringBuilder(type);
            bool first = true;
            foreach (string? part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                sb.Append(first ? ": " : ", ").Append(part.Trim());
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>The visible text inside <paramref name="container"/> (labels, buttons, checkbox labels), space separated; used for list rows.</summary>
        internal static string TextOf(UIContainer container)
        {
            var sb = new StringBuilder();
            foreach (UIElement e in container.SelfAndDescendants())
            {
                string? text = e switch
                {
                    Label label => label.CurrentText,
                    Button button => button.CurrentText,
                    Checkbox checkbox => checkbox.CurrentLabel,
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append(sb.Length > 0 ? " " : string.Empty).Append(text.Trim());
                }
            }
            return sb.ToString();
        }

        /// <summary>The framework's translated text for an accessibility key (<c>a11y.*</c> in i18n), falling back to <paramref name="fallback"/>.</summary>
        internal static string Text(string key, string fallback)
        {
            ITranslationHelper? translation = UIServices.Translation;
            if (translation == null)
            {
                return fallback;
            }

            Translation result = translation.Get("a11y." + key);
            return result.HasValue() ? result.ToString() : fallback;
        }
    }
}
