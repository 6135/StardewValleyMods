using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using StardewUIFramework.Tests.Testing;
using UIFramework.Api;
using Xunit;

namespace StardewUIFramework.Tests.Tests
{
    /// <summary>NumberInput, TextInput and Dropdown driven through clicks and keys.</summary>
    public class InputComponentTests
    {
        private static void ClickCenter(InputDriver input, IUIElement element) => input.Click(element.Bounds.Center.X, element.Bounds.Center.Y);

        [Fact]
        public void NumberInputStepsClampsAndParsesTyping()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            double value = 5;
            IUINumberInput number = host.Api.AddNumberInput(menu.Root, "n", () => value, v => value = v, 1, 28, 1, true);
            var changes = new List<double>();
            number.OnValueChanged = e => changes.Add(e.NewNumber);
            InputDriver input = host.Drive(menu);

            ClickCenter(input, number);
            Assert.True(number.IsFocused);

            input.Key(Keys.Up);
            Assert.Equal(6, value);

            input.Type("9");              // "69" clamps to 28
            Assert.Equal(28, value);
            input.Type("9");              // "289" clamps to 28 again: no change event
            Assert.Equal(28, value);

            input.Type("\b\b");           // "2", then empty → falls back to the lower bound
            Assert.Equal(1, value);
            input.Key(Keys.Down);         // cannot go below Min
            Assert.Equal(1, value);
            input.Type(".");              // no decimals accepted
            Assert.Equal(1, value);

            Assert.Equal(new double[] { 6, 28, 2, 1 }, changes);
        }

        [Fact]
        public void NumberInputWheelStepsWhileHoveredAndValidatorRejects()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            double value = 10;
            IUINumberInput number = host.Api.AddNumberInput(menu.Root, "n", () => value, v => value = v, 0, 100, 5, true);
            number.Validate = v => v <= 20;
            InputDriver input = host.Drive(menu);

            input.Hover(number.Bounds.Center.X, number.Bounds.Center.Y);
            input.Scroll(1);
            Assert.Equal(15, value);
            input.Scroll(1);
            Assert.Equal(20, value);
            input.Scroll(1);              // 25 is rejected by the validator
            Assert.Equal(20, value);
            input.Scroll(-1);
            Assert.Equal(15, value);
        }

        [Fact]
        public void TextInputEnforcesMaxLengthAndValidator()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            string text = string.Empty;
            IUITextInput field = host.Api.AddTextInput(menu.Root, "t", () => text, v => text = v);
            field.MaxLength = 3;
            field.Validate = v => !v.Contains('x');
            int changes = 0;
            field.OnValueChanged = _ => changes++;
            InputDriver input = host.Drive(menu);

            ClickCenter(input, field);
            input.Type("abcd");
            Assert.Equal("abc", text);
            Assert.Equal(3, changes);

            input.Type("\bx");            // backspace accepted, 'x' rejected
            Assert.Equal("ab", text);
            Assert.Equal(4, changes);

            input.Paste("zzzz");          // paste is truncated to MaxLength as one edit
            Assert.Equal("abz", text);
            Assert.Equal(5, changes);

            bool submitted = false;
            field.OnSubmit = _ => submitted = true;
            input.Key(Keys.Enter);
            Assert.True(submitted);
        }

        [Fact]
        public void TextInputPlaysTypingSoundsOnlyForAcceptedCharacters()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUITextInput field = host.Api.AddTextInput(menu.Root, "t", null!, null!);
            field.MaxLength = 1;
            InputDriver input = host.Drive(menu);

            ClickCenter(input, field);
            input.Type("ab");

            Assert.Equal("a", field.Value);
            Assert.Equal(new[] { "cowboy_monsterhit" }, host.Sounds);
        }

        [Fact]
        public void DropdownOpensSelectsAndClosesThroughClicks()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            string selected = "a";
            IUIDropdown dropdown = host.Api.AddDropdown(menu.Root, "d", () => new[] { "a", "b", "c" }, () => new[] { "A", "B", "C" }, () => selected, v => selected = v);
            host.Api.AddSpacer(menu.Root, "gap", 0, 200); // keeps the button clear of the open list
            int buttonClicks = 0;
            IUIButton button = host.Api.AddButton(menu.Root, "go", () => "Go", _ => buttonClicks++);
            var events = new List<string>();
            dropdown.OnValueChanged = e => events.Add(e.OldValue + ">" + e.NewValue);
            InputDriver input = host.Drive(menu);

            Assert.Equal(new Microsoft.Xna.Framework.Rectangle(490, 198, 300, 44), dropdown.Bounds);

            ClickCenter(input, dropdown);
            Assert.True(dropdown.IsOpen);
            Assert.Contains("shwip", host.Sounds);

            // the open list starts at the dropdown's top; row 2 is 88..132 px below it
            input.Click(dropdown.Bounds.X + 10, dropdown.Bounds.Y + 88 + 10);
            Assert.False(dropdown.IsOpen);
            Assert.Equal("c", selected);
            Assert.Equal(2, dropdown.SelectedIndex);
            Assert.Equal(new[] { "a>c" }, events);

            // clicking outside an open list closes it and swallows the click
            ClickCenter(input, dropdown);
            Assert.True(dropdown.IsOpen);
            ClickCenter(input, button);
            Assert.False(dropdown.IsOpen);
            Assert.Equal(0, buttonClicks);

            ClickCenter(input, button);
            Assert.Equal(1, buttonClicks);
        }

        [Fact]
        public void DropdownKeyboardChangesSelectionAndEscapeClosesTheList()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            IUIDropdown dropdown = host.Api.AddDropdown(menu.Root, "d", () => new[] { "a", "b", "c" }, null!, null!, null!);
            InputDriver input = host.Drive(menu);

            ClickCenter(input, dropdown);
            input.Key(Keys.Escape);
            Assert.False(dropdown.IsOpen);
            Assert.True(dropdown.IsFocused);

            input.Key(Keys.Down);
            Assert.Equal("b", dropdown.SelectedValue);
            input.Key(Keys.Enter);
            Assert.True(dropdown.IsOpen);
            input.Key(Keys.Down);
            input.Key(Keys.Enter);
            Assert.False(dropdown.IsOpen);
            Assert.Equal("c", dropdown.SelectedValue);
        }

        [Fact]
        public void CheckboxTogglesOnClickAndSpace()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            bool flag = false;
            IUICheckbox box = host.Api.AddCheckbox(menu.Root, "c", () => flag, v => flag = v);
            InputDriver input = host.Drive(menu);

            ClickCenter(input, box);
            Assert.True(flag);
            input.Key(Keys.Space);
            Assert.False(flag);
            Assert.Equal(new[] { "drumkit6", "drumkit6" }, host.Sounds);
        }

        [Fact]
        public void SliderSnapsToStepFromClicksAndKeys()
        {
            var host = new TestHost();
            IUIMenu menu = host.CreateBareMenu("m");
            double value = 0;
            IUISlider slider = host.Api.AddSlider(menu.Root, "s", () => value, v => value = v, 0, 100);
            slider.Step = 10;
            InputDriver input = host.Drive(menu);

            input.Drag(slider.Bounds.X, slider.Bounds.Center.Y, slider.Bounds.Right, slider.Bounds.Center.Y);
            Assert.Equal(100, value);
            input.Key(Keys.Left);
            Assert.Equal(90, value);
        }
    }
}
