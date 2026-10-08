using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using Xunit;

namespace UIFramework.Tests;

/// <summary>Data model JSON parsing: converters for actions, string lists, sources and elements, plus hashing and messages.</summary>
public class ModelTests
{
    private static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, DataAssetReader.Settings)!;

    [Fact]
    public void Menu_ReadsScalarsAndChildren()
    {
        var menu = Read<MenuDefinition>(@"{ ""Title"": ""Hello"", ""Width"": 400, ""Modal"": true, ""Children"": [ { ""Type"": ""Label"", ""Id"": ""a"", ""Text"": ""Hi"" }, { ""Type"": ""Button"", ""Id"": ""b"" } ] }");
        Assert.Equal("Hello", menu.Title);
        Assert.Equal("400", menu.Width);
        Assert.Equal("true", menu.Modal);
        Assert.Equal(2, menu.Children!.Count);
        Assert.Equal("Label", menu.Children[0].Type);
        Assert.Equal("b", menu.Children[1].Id);
    }

    [Fact]
    public void Element_SingleObjectChildren_BecomeList()
    {
        var single = Read<ElementDefinition>(@"{ ""Type"": ""List"", ""RowTemplate"": { ""Type"": ""Label"", ""Id"": ""x"" } }");
        Assert.Equal("x", Assert.Single(single.RowTemplate!).Id);
        var many = Read<ElementDefinition>(@"{ ""Type"": ""List"", ""RowTemplate"": [ { ""Id"": ""a"" }, { ""Id"": ""b"" } ] }");
        Assert.Equal(2, many.RowTemplate!.Count);
        Assert.Null(Read<ElementDefinition>(@"{ ""RowTemplate"": null }").RowTemplate);
    }

    [Fact]
    public void UnknownMembers_AreKept()
    {
        var element = Read<ElementDefinition>(@"{ ""Type"": ""Label"", ""Txet"": ""typo"" }");
        Assert.NotNull(element.Unknown);
        Assert.True(element.Unknown!.ContainsKey("Txet"));
    }

    [Fact]
    public void Actions_ReadAllAuthoringForms()
    {
        var action = Read<ActionDefinition>(@"{ ""Action"": ""AddMoney 1"", ""Condition"": ""PLAYER_HAS_MAIL Current x"", ""Actions"": [ ""A 1"", { ""Action"": ""B 2"", ""When"": ""menu.x"" }, [""C 3"", ""D 4""], null, ""  "" ], ""Else"": ""E 5"" }");
        Assert.Equal("AddMoney 1", action.Action);
        Assert.Equal(3, action.Actions!.Count);
        Assert.Equal("A 1", action.Actions[0].Action);
        Assert.True(action.Actions[0].IsPlain);
        Assert.Equal("menu.x", action.Actions[1].When);
        Assert.False(action.Actions[1].IsPlain);
        Assert.Equal(2, action.Actions[2].Actions!.Count);
        Assert.Single(action.Else!);
        Assert.Equal("E 5", action.Else![0].Action);
    }

    [Fact]
    public void Actions_NullIsNull() =>
        Assert.Null(Read<ActionDefinition>(@"{ ""Actions"": null }").Actions);

    [Fact]
    public void Actions_WriteBackPlainEntriesAsStrings()
    {
        var action = Read<ActionDefinition>(@"{ ""Actions"": [ ""A 1"", { ""Action"": ""B"", ""Condition"": ""C"" } ] }");
        JObject written = JObject.Parse(JsonConvert.SerializeObject(action, DataAssetReader.Settings));
        JArray actions = (JArray)written["Actions"]!;
        Assert.Equal(JTokenType.String, actions[0].Type);
        Assert.Equal(JTokenType.Object, actions[1].Type);
    }

    private sealed class Lists
    {
        [JsonConverter(typeof(StringListConverter))]
        public List<string>? Items { get; set; }
    }

    [Theory]
    [InlineData(@"{ ""Items"": ""spring, summer ,, fall"" }", new[] { "spring", "summer", "fall" })]
    [InlineData(@"{ ""Items"": [""a,b"", ""c""] }", new[] { "a,b", "c" })]
    [InlineData(@"{ ""Items"": """" }", new string[0])]
    [InlineData(@"{ ""Items"": [1, 2.5, null] }", new[] { "1", "2.5", "" })]
    public void StringList_Forms(string json, string[] expected) =>
        Assert.Equal(expected, Read<Lists>(json).Items);

    [Fact]
    public void StringList_NullAndRoundTrip()
    {
        Assert.Null(Read<Lists>(@"{ ""Items"": null }").Items);
        string json = JsonConvert.SerializeObject(new Lists { Items = new List<string> { "a", "b" } });
        Assert.Equal(new[] { "a", "b" }, Read<Lists>(json).Items);
    }

    private sealed class Texts
    {
        [JsonConverter(typeof(JsonTextConverter))]
        public string? Value { get; set; }
    }

    [Fact]
    public void JsonText_Converter()
    {
        Assert.Equal(@"[{""name"":""A""}]", Read<Texts>(@"{ ""Value"": [ { ""name"": ""A"" } ] }").Value);
        Assert.Equal("True", Read<Texts>(@"{ ""Value"": true }").Value);
        Assert.Equal("0.5", Read<Texts>(@"{ ""Value"": 0.5 }").Value);
        Assert.Null(Read<Texts>(@"{ ""Value"": null }").Value);
        Assert.Equal("hi", Read<Texts>(@"{ ""Value"": ""hi"" }").Value);
    }

    [Fact]
    public void JsonText_UsesInvariantCulture()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("-1.5", JsonText.Of(new JValue(-1.5)));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("${outer.items}", "Value")]
    [InlineData("themes", "Themes")]
    [InlineData("range:1..10", "Range")]
    [InlineData("query:ALL_ITEMS (O)", "ItemQuery")]
    [InlineData("asset:Data/Machines", "Asset")]
    [InlineData("hook:Name", "Hook")]
    [InlineData("state:menu.items", "State")]
    [InlineData("source:crops", "Named")]
    [InlineData("menu.items", "State")]
    public void SourceShorthand_Kinds(string text, string kind) =>
        Assert.Equal(kind, SourceConverter.Parse(text).Kind);

    [Fact]
    public void SourceShorthand_RangeParts()
    {
        SourceDefinition range = SourceConverter.Parse("range: 1 .. 10 .. 2");
        Assert.Equal("1", range.From);
        Assert.Equal("10", range.To);
        Assert.Equal("2", range.Step);
        Assert.Equal("5", SourceConverter.Parse("range:5").To);
    }

    [Fact]
    public void SourceShorthand_BareNameIsNamedAndState()
    {
        SourceDefinition bare = SourceConverter.Parse("crops");
        Assert.Equal("crops", bare.Name);
        Assert.Equal("crops", bare.State);
        Assert.Equal("crops", bare.Shorthand);
    }

    [Fact]
    public void Source_ReadsObjectArrayAndString()
    {
        var menu = Read<MenuDefinition>(@"{ ""Sources"": { ""a"": { ""From"": ""1"", ""To"": ""3"" }, ""b"": [ 1, 2, 3 ], ""c"": ""range:1..4"", ""d"": { ""Query"": ""ALL_ITEMS"" } } }");
        Assert.Equal("Range", menu.Sources!["a"].Kind);
        Assert.Equal("Rows", menu.Sources["b"].Kind);
        Assert.Equal(3, menu.Sources["b"].Rows!.Count);
        Assert.Equal("4", menu.Sources["c"].To);
        Assert.Equal("ItemQuery", menu.Sources["d"].Kind);
    }

    [Fact]
    public void SourceKinds_Canonical()
    {
        Assert.Equal("ItemQuery", SourceKinds.Canonical(" itemquery "));
        Assert.Null(SourceKinds.Canonical("nope"));
        Assert.Null(SourceKinds.Canonical(null));
    }

    [Fact]
    public void Source_KindInference()
    {
        Assert.Null(new SourceDefinition().Kind);
        Assert.Equal("Rows", new SourceDefinition { Rows = new List<JToken>() }.Kind);
        Assert.Equal("Range", new SourceDefinition { To = "3" }.Kind);
        Assert.Equal("Asset", new SourceDefinition { Asset = "a" }.Kind);
        Assert.Equal("Custom", new SourceDefinition { Type = " Custom " }.Kind);
    }

    [Fact]
    public void DefinitionHash_IsStableAndContentSensitive()
    {
        var a = Read<ElementDefinition>(@"{ ""Type"": ""Label"", ""Text"": ""x"" }");
        var b = Read<ElementDefinition>(@"{ ""Type"": ""Label"", ""Text"": ""x"" }");
        var c = Read<ElementDefinition>(@"{ ""Type"": ""Label"", ""Text"": ""y"" }");
        Assert.Equal(DefinitionHash.Of(a), DefinitionHash.Of(b));
        Assert.NotEqual(DefinitionHash.Of(a), DefinitionHash.Of(c));
        Assert.NotEqual(DefinitionHash.Of(a), DefinitionHash.Of(a, "extra"));
        Assert.Equal(32, DefinitionHash.Of(a).Length);
    }

    [Fact]
    public void Clone_IsDeepCopy()
    {
        var original = Read<MenuDefinition>(@"{ ""Title"": ""T"", ""Children"": [ { ""Type"": ""Label"", ""Text"": ""x"" } ] }");
        MenuDefinition copy = DataAssetReader.Clone(original);
        Assert.NotSame(original, copy);
        Assert.NotSame(original.Children, copy.Children);
        Assert.Equal("x", copy.Children![0].Text);
        copy.Children[0].Text = "changed";
        Assert.Equal("x", original.Children![0].Text);
    }

    [Fact]
    public void DataPath_BuildsReadablePaths()
    {
        DataPath path = DataPath.Entry("Menus", "Owner/demo").Field("Children").Index(2, "form").Field("Width");
        Assert.Equal("Menus[\"Owner/demo\"].Children[2](#form).Width", path.ToString());
        Assert.Equal("a[0]", DataPath.Entry("a", "k").Index(0, null).ToString().Replace("[\"k\"]", string.Empty));
    }

    [Fact]
    public void MessageLog_DeduplicatesAndCounts()
    {
        var log = new DataMessageLog();
        DataPath path = DataPath.Entry("Menus", "k");
        log.Error(path, "bad");
        log.Error(path, "bad");
        log.Warn(path, "hmm");
        log.Info(path, "fyi");
        Assert.Equal(3, log.Items.Count);
        Assert.Equal(1, log.Count(DataSeverity.Error));
        Assert.Equal("ERROR Menus[\"k\"]: bad", log.Items[0].ToString());

        var other = new DataMessageLog();
        other.Error(path, "bad");
        other.Error(path, "new");
        log.AddRange(other);
        Assert.Equal(4, log.Items.Count);

        log.Clear();
        Assert.Empty(log.Items);
        log.Error(path, "bad");
        Assert.Single(log.Items);
    }
}
