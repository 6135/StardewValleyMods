using System;
using System.ComponentModel.DataAnnotations;

namespace UIFrameworkExample
{
    /// <summary>Difficulty preset (enums become dropdowns of their names).</summary>
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard,
        VeryHard
    }

    // The framework matches form attributes by type name, so a consumer can declare its own instead of referencing
    // a shared assembly. [Range] below is the BCL one (System.ComponentModel.DataAnnotations).

    /// <summary>Starts a titled section before the property.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SectionAttribute : Attribute
    {
        public string Title { get; }

        public SectionAttribute(string title)
        {
            Title = title;
        }
    }

    /// <summary>Turns a string property into a dropdown of the comma separated values.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ChoicesAttribute : Attribute
    {
        public string Csv { get; }

        public ChoicesAttribute(string csv)
        {
            Csv = csv;
        }
    }

    /// <summary>Tooltip shown over the generated input.</summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class TooltipAttribute : Attribute
    {
        public string Text { get; }

        public TooltipAttribute(string text)
        {
            Text = text;
        }
    }

    /// <summary>A plain settings object; <c>AddForm</c> generates a complete form from it.</summary>
    public sealed class DemoSettings
    {
        [Section("Farm")]
        public string FarmName { get; set; } = "Stardew";

        [Range(1, 28)]
        public int Day { get; set; } = 1;

        [Choices("spring,summer,fall,winter")]
        public string Season { get; set; } = "spring";

        public bool Pets { get; set; } = true;

        [Section("Game")]
        [Tooltip("How hard the farm is: Easy to VeryHard.")]
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;

        /// <summary>Validators are found by name (<c>Validate</c> + property); returning a string reports an error, null / empty accepts.</summary>
        public string? ValidateFarmName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "The farm needs a name." : null;
        }
    }
}
