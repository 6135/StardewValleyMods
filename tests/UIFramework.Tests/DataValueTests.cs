using System.Collections.Generic;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using Xunit;

namespace UIFramework.Tests;

/// <summary>DataValue conversions, equality and ordering, plus the Numbers helpers it relies on.</summary>
public class DataValueTests
{
    private static DataValue List(params DataValue[] items) => DataValue.FromList(items);

    [Fact]
    public void Constructors_HandleNull()
    {
        Assert.True(DataValue.FromString(null).IsNull);
        Assert.True(DataValue.FromList(null).IsNull);
        Assert.True(DataValue.Opaque(null).IsNull);
        Assert.True(DataValue.FromObject(null).IsNull);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FromNumber_NonFiniteBecomesZero(double value) =>
        Assert.Equal(0, DataValue.FromNumber(value).AsNumber());

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" true ", true)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("0.0", false)]
    [InlineData("2.5", true)]
    [InlineData("", false)]
    [InlineData("hello", false)]
    public void AsBool_FromString(string text, bool expected) =>
        Assert.Equal(expected, DataValue.FromString(text).AsBool());

    [Fact]
    public void AsBool_OtherKinds()
    {
        Assert.False(DataValue.Null.AsBool());
        Assert.True(DataValue.FromNumber(0.5).AsBool());
        Assert.False(DataValue.FromNumber(0).AsBool());
        Assert.True(List(DataValue.FromNumber(1)).AsBool());
        Assert.False(List().AsBool());
        Assert.True(DataValue.Opaque(new object()).AsBool());
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-1.5", -1.5)]
    [InlineData("1e3", 1000)]
    [InlineData("true", 1)]
    [InlineData("False", 0)]
    [InlineData("abc", 0)]
    [InlineData("NaN", 0)]
    [InlineData("Infinity", 0)]
    [InlineData("", 0)]
    public void AsNumber_FromString(string text, double expected) =>
        Assert.Equal(expected, DataValue.FromString(text).AsNumber());

    [Fact]
    public void AsNumber_OtherKinds()
    {
        Assert.Equal(1, DataValue.True.AsNumber());
        Assert.Equal(0, DataValue.False.AsNumber());
        Assert.Equal(0, DataValue.Null.AsNumber());
        Assert.Equal(0, List(DataValue.FromNumber(5)).AsNumber());
    }

    private sealed class Named
    {
        private readonly string name;

        public Named(string name) => this.name = name;

        public override string ToString() => name;
    }

    [Fact]
    public void AsString_AllKinds()
    {
        Assert.Equal(string.Empty, DataValue.Null.AsString());
        Assert.Equal("True", DataValue.True.AsString());
        Assert.Equal("False", DataValue.False.AsString());
        Assert.Equal("3", DataValue.FromNumber(3).AsString());
        Assert.Equal("0.1", DataValue.FromNumber(0.1).AsString());
        Assert.Equal("0", DataValue.FromNumber(-0.0).AsString());
        Assert.Equal("1, a, True", List(DataValue.FromNumber(1), DataValue.FromString("a"), DataValue.True).AsString());
        Assert.Equal("obj", DataValue.Opaque(new Named("obj")).AsString());
    }

    [Fact]
    public void AsList_WrapsScalars()
    {
        Assert.Empty(DataValue.Null.AsList());
        IReadOnlyList<DataValue> single = DataValue.FromNumber(2).AsList();
        Assert.Single(single);
        Assert.Equal(2, single[0].AsNumber());
    }

    [Fact]
    public void AsObject_ReturnsPayload()
    {
        Assert.Null(DataValue.Null.AsObject());
        Assert.Equal(true, DataValue.True.AsObject());
        Assert.Equal(2.5, DataValue.FromNumber(2.5).AsObject());
        Assert.Equal("x", DataValue.FromString("x").AsObject());
    }

    [Fact]
    public void FromObject_MapsClrTypes()
    {
        Assert.Equal(DataValue.True, DataValue.FromObject(true));
        Assert.Equal(DataValue.FromNumber(3), DataValue.FromObject(3));
        Assert.Equal(DataValue.FromNumber(3), DataValue.FromObject(3L));
        Assert.Equal(DataValue.FromNumber(1.5), DataValue.FromObject(1.5f));
        Assert.Equal(DataValue.FromString("a"), DataValue.FromObject('a'));
        Assert.Equal(DataValue.FromString("Monday"), DataValue.FromObject(System.DayOfWeek.Monday));
        DataValue list = DataValue.FromObject(new[] { 1, 2, 3 });
        Assert.Equal(DataKind.List, list.Kind);
        Assert.Equal(3, list.AsList().Count);
    }

    [Fact]
    public void FromObject_CapsListLength()
    {
        var items = new int[ExpressionLimits.MaxListLength + 50];
        Assert.Equal(ExpressionLimits.MaxListLength, DataValue.FromObject(items).AsList().Count);
    }

    [Fact]
    public void Reactive_RoundTripsKinds()
    {
        Assert.Equal(DataValue.True, DataValue.FromReactive(DataValue.True.ToReactive()));
        Assert.Equal(DataValue.FromNumber(4), DataValue.FromReactive(DataValue.FromNumber(4).ToReactive()));
        Assert.Equal(DataValue.FromString("hi"), DataValue.FromReactive(DataValue.FromString("hi").ToReactive()));
        Assert.Equal(DataValue.FromString("1, 2"), DataValue.FromReactive(List(DataValue.FromNumber(1), DataValue.FromNumber(2)).ToReactive()));
    }

    [Theory]
    [InlineData(1.0, "1")]
    [InlineData(-2.5, "-2.5")]
    [InlineData(0.0, "0")]
    [InlineData(1e21, "1E+21")]
    public void NumberToText(double value, string expected) =>
        Assert.Equal(expected, DataValue.NumberToText(value));

    [Theory]
    [InlineData("1", true)]
    [InlineData(" 1 ", true)]
    [InlineData("1.", true)]
    [InlineData("+1", true)]
    [InlineData("1,5", false)]
    [InlineData("", false)]
    [InlineData("NaN", false)]
    [InlineData("0x10", false)]
    public void TryParseNumber(string text, bool ok) =>
        Assert.Equal(ok, DataValue.TryParseNumber(text, out _));

    [Fact]
    public void JoinList_UsesSeparator()
    {
        Assert.Equal(string.Empty, DataValue.JoinList(new DataValue[0], "-"));
        Assert.Equal("a", DataValue.JoinList(new[] { DataValue.FromString("a") }, "-"));
        Assert.Equal("a-b-c", DataValue.JoinList(new[] { DataValue.FromString("a"), DataValue.FromString("b"), DataValue.FromString("c") }, "-"));
    }

    [Fact]
    public void JoinList_StopsAtStringLimit()
    {
        var items = new DataValue[500];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = DataValue.FromString(new string('x', 100));
        }

        Assert.True(DataValue.JoinList(items, ",").Length <= ExpressionLimits.MaxStringLength + 200);
    }

    [Theory]
    [InlineData("1", "1.0", false)]
    [InlineData("a", "a", true)]
    [InlineData("a", "A", false)]
    [InlineData("a", "b", false)]
    public void LooseEquals_Strings(string a, string b, bool expected) =>
        Assert.Equal(expected, DataValue.LooseEquals(DataValue.FromString(a), DataValue.FromString(b)));

    [Fact]
    public void LooseEquals_NullOnlyEqualsNull()
    {
        Assert.True(DataValue.LooseEquals(DataValue.Null, DataValue.Null));
        Assert.False(DataValue.LooseEquals(DataValue.Null, DataValue.FromString(string.Empty)));
        Assert.False(DataValue.LooseEquals(DataValue.FromNumber(0), DataValue.Null));
    }

    [Fact]
    public void LooseEquals_MixedKinds()
    {
        Assert.True(DataValue.LooseEquals(DataValue.FromNumber(3), DataValue.FromString("3")));
        Assert.True(DataValue.LooseEquals(DataValue.FromString("3.0"), DataValue.FromNumber(3)));
        Assert.False(DataValue.LooseEquals(DataValue.FromNumber(3), DataValue.FromString("three")));
        Assert.True(DataValue.LooseEquals(DataValue.True, DataValue.FromString("true")));
        Assert.True(DataValue.LooseEquals(DataValue.True, DataValue.FromNumber(5)));
        Assert.False(DataValue.LooseEquals(DataValue.False, DataValue.FromNumber(1)));
    }

    [Fact]
    public void LooseEquals_Lists()
    {
        Assert.True(DataValue.LooseEquals(List(DataValue.FromNumber(1), DataValue.FromString("a")), List(DataValue.FromString("1"), DataValue.FromString("a"))));
        Assert.False(DataValue.LooseEquals(List(DataValue.FromNumber(1)), List(DataValue.FromNumber(1), DataValue.FromNumber(2))));
        Assert.False(DataValue.LooseEquals(List(DataValue.FromNumber(1)), List(DataValue.FromNumber(2))));
    }

    [Fact]
    public void LooseEquals_Objects()
    {
        var shared = new object();
        Assert.True(DataValue.LooseEquals(DataValue.Opaque(shared), DataValue.Opaque(shared)));
        Assert.False(DataValue.LooseEquals(DataValue.Opaque(new object()), DataValue.Opaque(new object())));
    }

    [Fact]
    public void Compare_NumbersAndText()
    {
        Assert.True(DataValue.Compare(DataValue.FromNumber(1), DataValue.FromNumber(2)) < 0);
        Assert.True(DataValue.Compare(DataValue.FromNumber(3), DataValue.FromNumber(2)) > 0);
        Assert.Equal(0, DataValue.Compare(DataValue.FromNumber(2), DataValue.FromNumber(2)));
        // numeric strings compare as numbers (so "10" > "9"), other strings ordinally
        Assert.True(DataValue.Compare(DataValue.FromString("10"), DataValue.FromString("9")) > 0);
        Assert.True(DataValue.Compare(DataValue.FromString("apple"), DataValue.FromString("banana")) < 0);
        Assert.Equal(0, DataValue.Compare(DataValue.FromString("a"), DataValue.FromString("a")));
    }

    [Fact]
    public void StrictEquality_RequiresSameKind()
    {
        DataValue one = DataValue.FromNumber(1);
        DataValue otherOne = DataValue.FromNumber(1);
        Assert.True(one == otherOne);
        Assert.True(DataValue.FromNumber(1) != DataValue.FromString("1"));
        Assert.Equal(DataValue.FromString("a").GetHashCode(), DataValue.FromString("a").GetHashCode());
        Assert.True(DataValue.Null == default);
    }

    [Fact]
    public void Numbers_Helpers()
    {
        Assert.True(Numbers.IsZero(0));
        Assert.True(Numbers.IsZero(-0.0));
        Assert.False(Numbers.IsZero(1e-300));
        Assert.True(Numbers.Same(1, 1 + 1e-12));
        Assert.False(Numbers.Same(1, 1.001));
    }
}
