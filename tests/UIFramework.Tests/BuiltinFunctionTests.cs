using UIFramework.Data.Expressions;
using Xunit;

namespace UIFramework.Tests;

/// <summary>Edge cases of the built-in functions (money, percent, format, join, substring, replace, quote).</summary>
public class BuiltinFunctionTests
{
    private static ExpressionResult Run(string source) =>
        CompiledExpression.Parse(source).Evaluate(ExpressionScope.Empty);

    private static string Text(string source)
    {
        ExpressionResult result = Run(source);
        Assert.True(result.Succeeded, result.Error);
        return result.Value.AsString();
    }

    [Theory]
    [InlineData(0, "0g")]
    [InlineData(999, "999g")]
    [InlineData(1234, "1,234g")]
    [InlineData(1234567.5, "1,234,568g")]
    [InlineData(-1500, "-1,500g")]
    [InlineData(0.4, "0g")]
    public void Money(double amount, string expected) =>
        Assert.Equal(expected, BuiltinFunctions.Money(amount));

    [Fact]
    public void Money_ClampsHugeValues()
    {
        Assert.Equal("1,000,000,000,000,000g", BuiltinFunctions.Money(1e30));
        Assert.Equal("-1,000,000,000,000,000g", BuiltinFunctions.Money(-1e30));
    }

    [Theory]
    [InlineData("percent(0.5)", "50%")]
    [InlineData("percent(0.256, 1)", "25.6%")]
    [InlineData("percent(1.5)", "150%")]
    [InlineData("percent(0)", "0%")]
    [InlineData("percent(-0.0001, 1)", "0.0%")]
    [InlineData("percent(0.125, 2)", "12.50%")]
    public void Percent(string source, string expected) => Assert.Equal(expected, Text(source));

    [Theory]
    [InlineData("format(1234.5, 'N1')", "1,234.5")]
    [InlineData("format(3.14159, 'F2')", "3.14")]
    [InlineData("format(7, '000')", "007")]
    public void Format(string source, string expected) => Assert.Equal(expected, Text(source));

    [Fact]
    public void Format_RejectsHugePrecision() =>
        Assert.False(Run("format(1, 'F999999')").Succeeded);

    [Theory]
    [InlineData("round(2.5)", 3)]
    [InlineData("round(-2.5)", -3)]
    [InlineData("min(1)", 1)]
    [InlineData("max(-1, -5)", -1)]
    public void Math(string source, double expected) =>
        Assert.Equal(expected, Run(source).Value.AsNumber(), 9);

    [Theory]
    [InlineData("substring('hello', -3)", "llo")]
    [InlineData("substring('hello', -3, 2)", "ll")]
    [InlineData("substring('hello', 10)", "")]
    [InlineData("substring('hello', 0, 100)", "hello")]
    [InlineData("substring('hello', 2, -1)", "")]
    public void Substring(string source, string expected) => Assert.Equal(expected, Text(source));

    [Theory]
    [InlineData("replace('aaa', 'a', 'bb')", "bbbbbb")]
    [InlineData("replace('abc', '', 'x')", "abc")]
    [InlineData("replace('', 'a', 'x')", "")]
    [InlineData("replace('Abc', 'a', 'x')", "Abc")]
    public void Replace(string source, string expected) => Assert.Equal(expected, Text(source));

    [Fact]
    public void Replace_RejectsRunawayGrowth()
    {
        string source = "replace('" + new string('a', 3000) + "', 'a', '" + new string('b', 9000) + "')";
        Assert.False(Run(source).Succeeded);
    }

    [Theory]
    [InlineData("join('x')", "x")]
    [InlineData("len('')", "0")]
    [InlineData("str(true)", "True")]
    [InlineData("str(2.50)", "2.5")]
    [InlineData("bool('false')", "False")]
    [InlineData("bool(1)", "True")]
    [InlineData("num(true)", "1")]
    public void Misc(string source, string expected) => Assert.Equal(expected, Text(source));

    [Fact]
    public void Quote_EscapesInnerQuotes() =>
        Assert.Equal("\"a\\\"b\"", BuiltinFunctions.Quote("a\"b"));

    [Theory]
    [InlineData("contains('abc', 'B')", false)]
    [InlineData("contains('abc', 'B', true)", true)]
    [InlineData("contains('abc', 'b')", true)]
    [InlineData("contains('abc', '')", true)]
    public void Contains(string source, bool expected) =>
        Assert.Equal(expected, Run(source).Value.AsBool());

    [Fact]
    public void Operators_ShortCircuit()
    {
        Assert.False(Run("false && noSuchFunction()").Value.AsBool());
        Assert.True(Run("true || noSuchFunction()").Value.AsBool());
    }

    [Fact]
    public void Strings_ConcatenateWithPlus() => Assert.Equal("ab1", Text("'a' + 'b' + 1"));
}
