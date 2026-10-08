using UIFramework.Data.Expressions;
using UIFramework.Data.State;
using Xunit;

namespace UIFramework.Tests;

/// <summary>State key parsing and the typed inference of stored text.</summary>
public class StateAddressTests
{
    private static StateAddress Parse(string text, bool allowBare = false)
    {
        Assert.True(StateAddress.TryParse(text, null, allowBare, out StateAddress address, out string error), error);
        return address;
    }

    private static string Fail(string text, bool allowBare = false)
    {
        Assert.False(StateAddress.TryParse(text, null, allowBare, out _, out string error));
        return error;
    }

    [Fact]
    public void Qualified_Menu()
    {
        StateAddress address = Parse("menu[Owner/demo].day");
        Assert.Equal(StateScope.Menu, address.Scope);
        Assert.Equal("Owner/demo", address.Container);
        Assert.Equal("day", address.Name);
        Assert.Equal("Owner", address.Owner);
        Assert.Equal("menu[Owner/demo].day", address.ToString());
    }

    [Fact]
    public void DottedNames_AreJoined()
    {
        StateAddress address = Parse("session[Owner].settings.day");
        Assert.Equal(StateScope.Session, address.Scope);
        Assert.Equal("settings.day", address.Name);
        Assert.Equal("Owner", address.Owner);
    }

    [Theory]
    [InlineData("player[Mod].x", (int)StateScope.Player)]
    [InlineData("config[Mod].x", (int)StateScope.Config)]
    public void OtherScopes(string text, int scopeValue)
    {
        StateAddress address = Parse(text);
        Assert.Equal((StateScope)scopeValue, address.Scope);
        Assert.Equal("Mod", address.Container);
    }

    [Fact]
    public void Stat_NeedsNoOwner()
    {
        StateAddress address = Parse("stat.daysPlayed");
        Assert.Equal(StateScope.Stat, address.Scope);
        Assert.Equal(string.Empty, address.Container);
        Assert.Equal(string.Empty, address.Owner);
        Assert.Equal("stat.daysPlayed", address.ToString());
    }

    [Fact]
    public void Stat_IgnoresQualifier() =>
        Assert.Equal(string.Empty, Parse("stat[x].y").Container);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_IsRejected(string text) => Assert.Contains("empty", Fail(text));

    [Fact]
    public void MenuQualifier_NeedsOwnerAndMenu() =>
        Assert.Contains("<owner>/<menu>", Fail("menu[nomenu].x"));

    [Fact]
    public void Unqualified_NeedsContext()
    {
        Assert.Contains("needs an owner", Fail("menu.x"));
        Assert.Contains("needs an owner", Fail("session.x"));
        Assert.Contains("With scope", Fail(".x"));
    }

    [Fact]
    public void Bare_RequiresPermissionAndMenu()
    {
        Assert.Contains("not a state key", Fail("x"));
        Assert.Contains("needs a menu", Fail("x", allowBare: true));
    }

    [Fact]
    public void NoValueName_IsRejected() =>
        Assert.Contains("no value name", Fail("menu[a/b]"));

    [Theory]
    [InlineData("1 + 2")]
    [InlineData("menu.")]
    [InlineData("'text'")]
    public void NotAPath_IsRejected(string text) => Assert.NotEmpty(Fail(text));

    [Fact]
    public void RootNames_RoundTrip()
    {
        foreach (StateScope scope in new[] { StateScope.Menu, StateScope.Session, StateScope.Player, StateScope.Stat, StateScope.Config })
        {
            Assert.True(StateAddress.TryGetScope(StateAddress.RootName(scope), out StateScope back));
            Assert.Equal(scope, back);
        }

        Assert.False(StateAddress.TryGetScope("ctx", out _));
    }

    [Fact]
    public void JoinName_Ranges()
    {
        var path = new[] { new PathSegment("a", false), new PathSegment("b", true), new PathSegment("c", false) };
        Assert.Equal("a.b.c", StateAddress.JoinName(path, 0));
        Assert.Equal("c", StateAddress.JoinName(path, 2));
        Assert.Equal("b.c", StateAddress.JoinName(path, 1, 3));
        Assert.Equal(string.Empty, StateAddress.JoinName(path, 3));
        Assert.Equal("a[b].c", PathSegment.Format(path));
    }

    [Fact]
    public void Infer_TypesText()
    {
        Assert.Equal(DataValue.FromNumber(7), StateAddress.Infer("7"));
        Assert.Equal(DataValue.FromNumber(7), StateAddress.Infer(" 007 "));
        Assert.Equal(DataValue.True, StateAddress.Infer("TRUE"));
        Assert.Equal(DataValue.False, StateAddress.Infer("false"));
        Assert.Equal(DataValue.FromString("hello"), StateAddress.Infer("hello"));
        Assert.True(StateAddress.Infer(null).IsNull);
    }

    [Theory]
    [InlineData("7", (int)DataKind.Number)]
    [InlineData("007", (int)DataKind.String)]
    [InlineData("1.", (int)DataKind.String)]
    [InlineData("+1", (int)DataKind.String)]
    [InlineData("0.5", (int)DataKind.Number)]
    [InlineData("true", (int)DataKind.Bool)]
    [InlineData("abc", (int)DataKind.String)]
    public void FromStoredText_OnlyExactNumbers(string text, int kind) =>
        Assert.Equal((DataKind)kind, StateAddress.FromStoredText(text).Kind);

    [Fact]
    public void FromStoredText_NullIsNull() => Assert.True(StateAddress.FromStoredText(null).IsNull);
}
