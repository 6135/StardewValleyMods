using UIFramework.Data.Expressions;
using Xunit;

namespace UIFramework.Tests;

/// <summary>The data expression language: parsing, operators and the pure built-in functions.</summary>
public class ExpressionTests
{
    private static ExpressionResult Run(string source) =>
        CompiledExpression.Parse(source).Evaluate(ExpressionScope.Empty);

    private static DataValue Value(string source)
    {
        ExpressionResult result = Run(source);
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 / 4", 2.5)]
    [InlineData("10 % 3", 1)]
    [InlineData("-5 + 2", -3)]
    [InlineData("true ? 1 : 2", 1)]
    [InlineData("false ? 1 : 2", 2)]
    [InlineData("round(2.345, 2)", 2.35)]
    [InlineData("floor(2.9)", 2)]
    [InlineData("ceil(2.1)", 3)]
    [InlineData("abs(-4)", 4)]
    [InlineData("min(3, 1, 2)", 1)]
    [InlineData("max(3, 1, 2)", 3)]
    [InlineData("clamp(15, 0, 10)", 10)]
    [InlineData("clamp(-1, 0, 10)", 0)]
    [InlineData("len('hello')", 5)]
    [InlineData("num('42')", 42)]
    public void Numbers(string source, double expected) =>
        Assert.Equal(expected, Value(source).AsNumber(), 9);

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("2 <= 1", false)]
    [InlineData("3 == 3", true)]
    [InlineData("3 != 3", false)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("!true", false)]
    [InlineData("contains('Stardew', 'dew')", true)]
    [InlineData("contains('Stardew', 'DEW', true)", true)]
    public void Booleans(string source, bool expected) =>
        Assert.Equal(expected, Value(source).AsBool());

    [Theory]
    [InlineData("upper('abc')", "ABC")]
    [InlineData("lower('ABC')", "abc")]
    [InlineData("trim('  x  ')", "x")]
    [InlineData("replace('a-b-c', '-', '+')", "a+b+c")]
    [InlineData("substring('farmer', 1, 3)", "arm")]
    [InlineData("str(1.5)", "1.5")]
    public void Strings(string source, string expected) =>
        Assert.Equal(expected, Value(source).AsString());

    [Fact]
    public void UnknownPath_IsNull() =>
        Assert.Equal(DataValue.Null, Value("menu.missing"));

    [Theory]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 ? 2")]
    public void InvalidSyntax_ReportsError(string source) =>
        Assert.False(Run(source).Succeeded);

    [Fact]
    public void UnknownFunction_ReportsError() =>
        Assert.False(Run("noSuchFunction(1)").Succeeded);
}
