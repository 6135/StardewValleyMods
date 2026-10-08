using System;
using System.Linq;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using UIFramework.Core;
using Xunit;

namespace UIFramework.Tests;

/// <summary>AutoForm model reflection: field kinds, labels, ranges, choices and validators.</summary>
public class FormReflectionTests
{
    private enum Mode
    {
        Fast,
        Slow
    }

    [AttributeUsage(AttributeTargets.Property)]
    private sealed class ChoicesAttribute : Attribute
    {
        public ChoicesAttribute(string csv) => Csv = csv;

        public string Csv { get; }
    }

    private class BaseModel
    {
        public bool BaseFlag { get; set; }
    }

    private sealed class SampleModel : BaseModel
    {
        public int Count { get; set; }

        [DisplayName("Custom Label")]
        [Description("not used for tooltip")]
        public string FarmName { get; set; } = string.Empty;

        [Range(1, 10)]
        public double UIScale { get; set; } = 1;

        [Range(10, 1)]
        public int Reversed { get; set; }

        [Choices("a, b ,c")]
        public string Pick { get; set; } = "a";

        public Mode Speed { get; set; }

        public float Ratio { get; set; }

        public int ReadOnlyValue { get; } = 3;

        public bool ValidateCount(int value) => value >= 0;

        public string ValidateFarmName() => FarmName.Length > 3 ? "too long" : string.Empty;
    }

    private static FormProperty Find(FormProperty[] properties, string name) =>
        properties.Single(p => p.Name == name);

    [Fact]
    public void Describe_OrdersBaseFirstAndSkipsReadOnly()
    {
        FormProperty[] properties = FormReflection.Describe(new SampleModel());
        Assert.Equal("BaseFlag", properties[0].Name);
        Assert.DoesNotContain(properties, p => p.Name == "ReadOnlyValue");
        Assert.Equal(new[] { "BaseFlag", "Count", "FarmName", "UIScale", "Reversed", "Pick", "Speed", "Ratio" }, properties.Select(p => p.Name));
    }

    [Fact]
    public void Kinds()
    {
        FormProperty[] properties = FormReflection.Describe(new SampleModel());
        Assert.Equal(FormFieldKind.Bool, Find(properties, "BaseFlag").Kind);
        Assert.Equal(FormFieldKind.Number, Find(properties, "Count").Kind);
        Assert.Equal(FormFieldKind.Text, Find(properties, "FarmName").Kind);
        Assert.Equal(FormFieldKind.Choice, Find(properties, "Pick").Kind);
        Assert.Equal(FormFieldKind.Enum, Find(properties, "Speed").Kind);
        Assert.True(Find(properties, "Count").IsInteger);
        Assert.False(Find(properties, "Ratio").IsInteger);
    }

    [Fact]
    public void Labels()
    {
        FormProperty[] properties = FormReflection.Describe(new SampleModel());
        Assert.Equal("Custom Label", Find(properties, "FarmName").Label);
        Assert.Equal("UI Scale", Find(properties, "UIScale").Label);
        Assert.Equal("Base Flag", Find(properties, "BaseFlag").Label);
    }

    [Fact]
    public void Ranges()
    {
        FormProperty[] properties = FormReflection.Describe(new SampleModel());
        Assert.Equal(1, Find(properties, "UIScale").Min);
        Assert.Equal(10, Find(properties, "UIScale").Max);
        // a reversed Range is normalised
        Assert.Equal(1, Find(properties, "Reversed").Min);
        Assert.Equal(10, Find(properties, "Reversed").Max);
        Assert.Equal(int.MinValue, Find(properties, "Count").Min);
        Assert.Equal(int.MaxValue, Find(properties, "Count").Max);
    }

    [Fact]
    public void Choices()
    {
        FormProperty[] properties = FormReflection.Describe(new SampleModel());
        Assert.Equal(new[] { "a", "b", "c" }, Find(properties, "Pick").Choices);
        Assert.Equal(new[] { "Fast", "Slow" }, Find(properties, "Speed").Choices);
    }

    [Fact]
    public void GetterAndSetter_ReadAndWrite()
    {
        var model = new SampleModel { Count = 4 };
        FormProperty count = Find(FormReflection.Describe(model), "Count");
        Assert.Equal(4, count.Getter(model));
        count.Setter(model, 9);
        Assert.Equal(9, model.Count);
    }

    [Fact]
    public void Validation_WithValueParameter()
    {
        var model = new SampleModel();
        FormProperty count = Find(FormReflection.Describe(model), "Count");
        Assert.Null(FormReflection.Validate(model, count, 5, "invalid"));
        Assert.Equal("invalid", FormReflection.Validate(model, count, -1, "invalid"));
    }

    [Fact]
    public void Validation_ParameterlessSeesValueAndRestoresModel()
    {
        var model = new SampleModel { FarmName = "ok" };
        FormProperty name = Find(FormReflection.Describe(model), "FarmName");
        Assert.Equal("too long", FormReflection.Validate(model, name, "way too long", "invalid"));
        Assert.Null(FormReflection.Validate(model, name, "abc", "invalid"));
        Assert.Equal("ok", model.FarmName);
    }

    [Fact]
    public void Validation_NoValidator_IsValid()
    {
        var model = new SampleModel();
        FormProperty ratio = Find(FormReflection.Describe(model), "Ratio");
        Assert.Null(FormReflection.Validate(model, ratio, 1f, "invalid"));
    }

    [Fact]
    public void Validation_CustomValidatorWins()
    {
        var model = new SampleModel();
        FormProperty count = Find(FormReflection.Describe(model), "Count");
        count.CustomValidator = (_, value) => (int)value! > 100 ? "too big" : null;
        Assert.Equal("too big", FormReflection.Validate(model, count, 101, "invalid"));
        Assert.Null(FormReflection.Validate(model, count, -5, "invalid"));
    }

    [Fact]
    public void AccessorBackedField_UsesGivenAccessors()
    {
        int stored = 0;
        var field = new FormProperty("MyField", typeof(int), FormFieldKind.Number, _ => stored, (_, v) => stored = (int)v!);
        Assert.Equal("My Field", field.Label);
        Assert.Null(field.Property);
        field.Setter(new object(), 5);
        Assert.Equal(5, field.Getter(new object()));
    }

    [Theory]
    [InlineData("FarmName", "Farm Name")]
    [InlineData("UIScale", "UI Scale")]
    [InlineData("Day2", "Day 2")]
    [InlineData("my_field", "my field")]
    [InlineData("A", "A")]
    [InlineData("", "")]
    [InlineData("HTTPServer", "HTTP Server")]
    [InlineData("lowercase", "lowercase")]
    public void SplitCamelCase(string name, string expected) =>
        Assert.Equal(expected, FormReflection.SplitCamelCase(name));

    [Fact]
    public void IsIntegerType()
    {
        Assert.True(FormReflection.IsIntegerType(typeof(int)));
        Assert.True(FormReflection.IsIntegerType(typeof(long)));
        Assert.False(FormReflection.IsIntegerType(typeof(double)));
        Assert.False(FormReflection.IsIntegerType(typeof(short)));
    }
}
