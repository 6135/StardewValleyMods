using System;
using System.Collections.Generic;
using UIFramework.Data.Expressions;
using Xunit;

namespace UIFramework.Tests;

/// <summary>Text templates (<c>${}</c>, <c>$:{}</c>, <c>$${</c>), the function registry and expression parse errors.</summary>
public class TemplateTests
{
    /// <summary>A scope that resolves <c>menu.&lt;name&gt;</c> from a dictionary.</summary>
    private sealed class DictionaryScope : IExpressionScope
    {
        private readonly Dictionary<string, DataValue> values;

        public DictionaryScope(Dictionary<string, DataValue> values) => this.values = values;

        public bool TryResolve(IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile)
        {
            isVolatile = false;
            value = DataValue.Null;
            return path.Count == 2 && path[0].Key == "menu" && values.TryGetValue(path[1].Key, out value);
        }

        public bool TryGetMember(DataValue target, PathSegment member, out DataValue value, out bool isVolatile)
        {
            value = DataValue.Null;
            isVolatile = false;
            return false;
        }

        public bool TryCallExternal(string name, ReadOnlySpan<DataValue> args, out DataValue result, out bool isVolatile)
        {
            isVolatile = false;
            result = DataValue.Null;
            if (name != "mod/twice" || args.Length != 1)
            {
                return false;
            }

            result = DataValue.FromNumber(args[0].AsNumber() * 2);
            return true;
        }
    }

    private static DictionaryScope Scope(params (string, DataValue)[] pairs)
    {
        var dictionary = new Dictionary<string, DataValue>();
        foreach ((string key, DataValue value) in pairs)
        {
            dictionary[key] = value;
        }

        return new DictionaryScope(dictionary);
    }

    [Fact]
    public void StaticText_HasNoExpressions()
    {
        Template template = Template.Parse("just text");
        Assert.True(template.IsStatic);
        Assert.True(template.IsConstant);
        Assert.Equal("just text", template.StaticText);
        Assert.Empty(template.Errors);
    }

    [Fact]
    public void EscapedDollar_IsLiteral()
    {
        Template template = Template.Parse("cost $${5}");
        Assert.True(template.IsStatic);
        Assert.Equal("cost ${5}", template.StaticText);
    }

    [Fact]
    public void NullText_IsEmptyStatic()
    {
        Template template = Template.Parse(null);
        Assert.True(template.IsStatic);
        Assert.Equal(string.Empty, template.StaticText);
    }

    [Fact]
    public void Parse_ReusesCachedTemplates() =>
        Assert.Same(Template.Parse("same ${1 + 1}"), Template.Parse("same ${1 + 1}"));

    [Fact]
    public void Interpolates_LiveValues()
    {
        Template template = Template.Parse("Hello ${menu.name}, you have ${menu.n * 2} coins");
        Assert.False(template.IsStatic);
        Assert.False(template.IsSingleExpression);
        ExpressionResult result = template.Evaluate(Scope(("name", DataValue.FromString("Ann")), ("n", DataValue.FromNumber(4))));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Hello Ann, you have 8 coins", result.Value.AsString());
    }

    [Fact]
    public void ConstantExpression_IsConstantTemplate()
    {
        Template template = Template.Parse("${1 + 2}");
        Assert.True(template.IsConstant);
        Assert.Equal(3, template.Evaluate(ExpressionScope.Empty).Value.AsNumber());
    }

    [Fact]
    public void SingleExpression_KeepsType()
    {
        Template template = Template.Parse("${menu.n > 3}");
        Assert.True(template.IsSingleExpression);
        ExpressionResult result = template.Evaluate(Scope(("n", DataValue.FromNumber(5))));
        Assert.Equal(DataKind.Bool, result.Value.Kind);
        Assert.True(result.Value.AsBool());
    }

    [Fact]
    public void OneTime_Segments()
    {
        Template template = Template.Parse("a $:{menu.n} b");
        Assert.True(template.HasOneTime);
        Assert.False(template.IsConstant);
        Template resolved = template.ResolveOneTime(Scope(("n", DataValue.FromNumber(7))), out string? error);
        Assert.Null(error);
        Assert.False(resolved.HasOneTime);
        Assert.Equal("a 7 b", resolved.Evaluate(ExpressionScope.Empty).Value.AsString());
    }

    [Fact]
    public void UnterminatedExpression_ReportsError()
    {
        Template template = Template.Parse("x ${1 +");
        Assert.NotEmpty(template.Errors);
    }

    [Fact]
    public void ParseField_BareExpressionAndTemplate()
    {
        Template bare = Template.ParseField("2 * 4");
        Assert.True(bare.IsSingleExpression);
        Assert.Equal(8, bare.Evaluate(ExpressionScope.Empty).Value.AsNumber());

        Template wrapped = Template.ParseField("${menu.n}px");
        Assert.Equal("5px", wrapped.Evaluate(Scope(("n", DataValue.FromNumber(5)))).Value.AsString());

        Template blank = Template.ParseField("  ");
        Assert.Equal("  ", blank.Evaluate(ExpressionScope.Empty).Value.AsString());
    }

    [Fact]
    public void ParseField_InvalidExpression_ReportsError() =>
        Assert.NotEmpty(Template.ParseField("1 +").Errors);

    [Fact]
    public void ExternalFunctionCalls_GoThroughScope()
    {
        ExpressionResult result = CompiledExpression.Parse("@mod/twice(21)").Evaluate(Scope());
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(42, result.Value.AsNumber());
    }

    [Fact]
    public void UnknownExternalFunction_Fails() =>
        Assert.False(CompiledExpression.Parse("@mod/none(1)").Evaluate(Scope()).Succeeded);

    [Fact]
    public void ConstantFolding_Flags()
    {
        Assert.True(CompiledExpression.Parse("1 + 2").IsConstant);
        Assert.False(CompiledExpression.Parse("menu.n + 2").IsConstant);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1 2")]
    [InlineData("'unterminated")]
    [InlineData("1 +* 2")]
    [InlineData(")")]
    [InlineData("abs(1,")]
    [InlineData("1 ? 2 :")]
    public void ParseErrors_AreReported(string source)
    {
        CompiledExpression expression = CompiledExpression.Compile(source);
        Assert.False(expression.IsValid);
        Assert.NotNull(expression.Error);
        Assert.True(expression.Error!.Position >= 0);
        Assert.False(expression.Evaluate(ExpressionScope.Empty).Succeeded);
    }

    [Fact]
    public void TooLongSource_IsRejected()
    {
        string source = new string('1', ExpressionLimits.MaxSourceLength + 1);
        Assert.False(CompiledExpression.Compile(source).IsValid);
    }

    [Fact]
    public void TooDeeplyNested_IsRejected()
    {
        string source = new string('(', 200) + "1" + new string(')', 200);
        Assert.False(CompiledExpression.Compile(source).IsValid);
    }

    [Fact]
    public void ParseError_OffsetShifts()
    {
        var error = new ParseError("bad", 3);
        Assert.Equal(8, error.WithOffset(5).Position);
        Assert.Same(error, error.WithOffset(0));
        Assert.Contains("position 3", error.ToString());
    }

    [Fact]
    public void FunctionRegistry_RegisterValidatesNameAndArity()
    {
        var registry = new FunctionRegistry();
        ExpressionFunction f = (_, _) => DataValue.Null;
        Assert.True(registry.Register("good_name1", 0, 0, f));
        Assert.True(registry.Register("_under", 0, -1, f));
        Assert.False(registry.Register("1bad", 0, 0, f));
        Assert.False(registry.Register("has space", 0, 0, f));
        Assert.False(registry.Register(string.Empty, 0, 0, f));
        Assert.False(registry.Register("neg", -1, 0, f));
        Assert.False(registry.Register("range", 3, 1, f));
    }

    [Fact]
    public void FunctionRegistry_IsCaseInsensitive_AndVersioned()
    {
        var registry = new FunctionRegistry();
        int before = registry.Version;
        registry.Register("Twice", 1, 1, (_, a) => DataValue.FromNumber(a[0].AsNumber() * 2));
        Assert.True(registry.Version > before);
        Assert.True(registry.TryGet("twice", out FunctionDefinition? function));
        Assert.Equal(1, function!.MinArguments);
        Assert.False(registry.TryGet("missing", out _));

        Assert.Equal(10, CompiledExpression.Parse("TWICE(5)").Evaluate(ExpressionScope.Empty, registry).Value.AsNumber());
    }

    [Fact]
    public void FunctionRegistry_Volatile_MarksResult()
    {
        var registry = new FunctionRegistry();
        registry.Register("now", 0, 0, (_, _) => DataValue.FromNumber(1), isVolatile: true);
        Assert.True(CompiledExpression.Parse("now()").Evaluate(ExpressionScope.Empty, registry).IsVolatile);
        Assert.False(CompiledExpression.Parse("1 + 1").Evaluate(ExpressionScope.Empty, registry).IsVolatile);
    }

    [Fact]
    public void Default_HasBuiltins()
    {
        foreach (string name in new[] { "round", "floor", "ceil", "abs", "min", "max", "clamp", "format", "money", "percent", "len", "upper", "lower", "trim", "contains", "replace", "substring", "join", "quote", "num", "str", "bool" })
        {
            Assert.True(FunctionRegistry.Default.TryGet(name, out _), name);
        }
    }

    [Fact]
    public void Arity_IsChecked()
    {
        Assert.False(CompiledExpression.Parse("abs()").Evaluate(ExpressionScope.Empty).Succeeded);
        Assert.False(CompiledExpression.Parse("abs(1, 2)").Evaluate(ExpressionScope.Empty).Succeeded);
    }
}
