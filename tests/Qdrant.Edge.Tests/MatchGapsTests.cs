using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Match predicate pins: factories, JSON round-trips, and native counts.</summary>
public sealed class MatchGapsTests
{
    [Fact]
    public void MatchCreateValue_String_Preserved()
    {
        Match match = Match.CreateValue(ValueVariants.String("hello"));
        ValueMatch value = Assert.IsType<ValueMatch>(match);
        StringValueVariant inner = Assert.IsType<StringValueVariant>(value.Value);
        Assert.Equal("hello", inner.Value);
    }

    [Fact]
    public void MatchCreateValue_Integer_Preserved()
    {
        Match match = Match.CreateValue(ValueVariants.Integer(42));
        ValueMatch value = Assert.IsType<ValueMatch>(match);
        IntegerValueVariant inner = Assert.IsType<IntegerValueVariant>(value.Value);
        Assert.Equal(42, inner.Value);
    }

    [Fact]
    public void MatchCreateValue_Bool_Preserved()
    {
        Match match = Match.CreateValue(ValueVariants.Bool(true));
        ValueMatch value = Assert.IsType<ValueMatch>(match);
        BoolValueVariant inner = Assert.IsType<BoolValueVariant>(value.Value);
        Assert.True(inner.Value);
    }

    [Fact]
    public void MatchCreateText_Preserved()
    {
        Match match = Match.CreateText("hello world");
        TextMatch text = Assert.IsType<TextMatch>(match);
        Assert.Equal("hello world", text.Text);
    }

    [Fact]
    public void MatchCreateTextAny_Preserved()
    {
        Match match = Match.CreateTextAny("hello world");
        TextAnyMatch textAny = Assert.IsType<TextAnyMatch>(match);
        Assert.Equal("hello world", textAny.TextAny);
    }

    [Fact]
    public void MatchCreatePhrase_Preserved()
    {
        Match match = Match.CreatePhrase("hello world");
        PhraseMatch phrase = Assert.IsType<PhraseMatch>(match);
        Assert.Equal("hello world", phrase.Phrase);
    }

    [Fact]
    public void MatchCreatePrefix_Preserved()
    {
        Match match = Match.CreatePrefix("hel");
        PrefixMatch prefix = Assert.IsType<PrefixMatch>(match);
        Assert.Equal("hel", prefix.Prefix);
    }

    [Fact]
    public void MatchCreateSubstring_Preserved()
    {
        Match match = Match.CreateSubstring("ell");
        SubstringMatch substring = Assert.IsType<SubstringMatch>(match);
        Assert.Equal("ell", substring.Substring);
    }

    [Fact]
    public void MatchCreateAny_Empty_Passes()
    {
        Match strings = Match.CreateAny(AnyVariants.Strings([]));
        Match integers = Match.CreateAny(AnyVariants.Integers([]));
        Assert.IsType<AnyMatch>(strings);
        Assert.IsType<AnyMatch>(integers);
    }

    [Fact]
    public void MatchCreateAny_Strings_Preserved()
    {
        Match match = Match.CreateAny(AnyVariants.Strings(["a", "b"]));
        AnyMatch any = Assert.IsType<AnyMatch>(match);
        StringsAnyVariant inner = Assert.IsType<StringsAnyVariant>(any.Any);
        Assert.Equal(["a", "b"], inner.Values);
    }

    [Fact]
    public void MatchCreateAny_Integers_Preserved()
    {
        Match match = Match.CreateAny(AnyVariants.Integers([1, 2]));
        AnyMatch any = Assert.IsType<AnyMatch>(match);
        IntegersAnyVariant inner = Assert.IsType<IntegersAnyVariant>(any.Any);
        Assert.Equal([1L, 2L], inner.Values);
    }

    [Fact]
    public void MatchCreateExcept_Preserved()
    {
        Match match = Match.CreateExcept(AnyVariants.Strings(["a"]));
        ExceptMatch except = Assert.IsType<ExceptMatch>(match);
        StringsAnyVariant inner = Assert.IsType<StringsAnyVariant>(except.Except);
        Assert.Equal(["a"], inner.Values);
    }

    [Fact]
    public void Match_All8Kinds_RoundTripThroughJson()
    {
        Match[] kinds =
        [
            Match.CreateValue(ValueVariants.String("a")),
            Match.CreateText("alpha bravo"),
            Match.CreateTextAny("alpha bravo"),
            Match.CreatePhrase("alpha bravo"),
            Match.CreatePrefix("alp"),
            Match.CreateSubstring("lph"),
            Match.CreateAny(AnyVariants.Strings(["a", "b"])),
            Match.CreateExcept(AnyVariants.Integers([1, 2])),
        ];
        foreach (Match original in kinds)
        {
            string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Match);
            Match? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Match);
            Assert.NotNull(back);
            Assert.IsType(original.GetType(), back);
            AssertMatchEqual(original, back);
        }
    }

    [Fact]
    public void MatchValue_Bool_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedKeywordShard(
            out _,
            "active",
            [
                BoolPoint(1, true),
                BoolPoint(2, true),
                BoolPoint(3, false),
            ]);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("active", Match.CreateValue(ValueVariants.Bool(true))) }));
    }

    [Fact]
    public void MatchValue_Integer_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("count", PayloadSchemaType.Integer));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                IntPoint(1, 42),
                IntPoint(2, 42),
                IntPoint(3, 7),
            ]));
        }
        catch
        {
            shard.Dispose();
            throw;
        }

        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("count", Match.CreateValue(ValueVariants.Integer(42))) }));
    }

    [Fact]
    public void MatchTextAny_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedTextShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("title", Match.CreateTextAny("alpha")) }));
    }

    [Fact]
    public void MatchPhrase_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedTextShard(out _, phraseMatching: true);
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = MatchFilter("title", Match.CreatePhrase("alpha bravo")) }));
    }

    [Fact]
    public void MatchPrefix_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedTextShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("title", Match.CreatePrefix("alp")) }));
    }

    [Fact]
    public void MatchSubstring_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedTextShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("title", Match.CreateSubstring("lpha")) }));
    }

    [Fact]
    public void MatchAny_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedKeywordShard(
            out _,
            "category",
            [
                CategoryPoint(1, "a"),
                CategoryPoint(2, "b"),
                CategoryPoint(3, "c"),
            ]);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("category", Match.CreateAny(AnyVariants.Strings(["a", "b"]))) }));
    }

    [Fact]
    public void MatchExcept_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedKeywordShard(
            out _,
            "category",
            [
                CategoryPoint(1, "a"),
                CategoryPoint(2, "b"),
                CategoryPoint(3, "c"),
            ]);
        Assert.Equal(2ul, shard.Count(new CountRequest { Filter = MatchFilter("category", Match.CreateExcept(AnyVariants.Strings(["a"]))) }));
    }

    private static Filter MatchFilter(string key, Match match) => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = key, Match = match })],
    };

    private static void AssertMatchEqual(Match expected, Match actual)
    {
        switch (expected, actual)
        {
            case (ValueMatch o, ValueMatch b):
                Assert.Equal(o.Value, b.Value);
                break;
            case (TextMatch o, TextMatch b):
                Assert.Equal(o.Text, b.Text);
                break;
            case (TextAnyMatch o, TextAnyMatch b):
                Assert.Equal(o.TextAny, b.TextAny);
                break;
            case (PhraseMatch o, PhraseMatch b):
                Assert.Equal(o.Phrase, b.Phrase);
                break;
            case (PrefixMatch o, PrefixMatch b):
                Assert.Equal(o.Prefix, b.Prefix);
                break;
            case (SubstringMatch o, SubstringMatch b):
                Assert.Equal(o.Substring, b.Substring);
                break;
            case (AnyMatch o, AnyMatch b):
                AssertAnyEqual(o.Any, b.Any);
                break;
            case (ExceptMatch o, ExceptMatch b):
                AssertAnyEqual(o.Except, b.Except);
                break;
            default:
                Assert.Fail("mismatched match kinds");
                break;
        }
    }

    private static void AssertAnyEqual(AnyVariants expected, AnyVariants actual)
    {
        switch (expected, actual)
        {
            case (StringsAnyVariant o, StringsAnyVariant b):
                Assert.Equal(o.Values, b.Values);
                break;
            case (IntegersAnyVariant o, IntegersAnyVariant b):
                Assert.Equal(o.Values, b.Values);
                break;
            default:
                Assert.Fail("mismatched any variants");
                break;
        }
    }

    private static EdgeShard SeedKeywordShard(out string dir, string field, IReadOnlyList<Point> points)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex(field, PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.UpsertPoints(points));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static EdgeShard SeedTextShard(out string dir, bool phraseMatching = false)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndexWithParams(
                "title",
                PayloadIndexParams.Text(new TextIndexParams { PhraseMatching = phraseMatching ? true : null })));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TitlePoint(1, "alpha bravo"),
                TitlePoint(2, "alpha charlie bravo"),
                TitlePoint(3, "delta echo"),
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static Point CategoryPoint(ulong id, string category) =>
        TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["category"] = category },
        };

    private static Point BoolPoint(ulong id, bool active) =>
        TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["active"] = active },
        };

    private static Point IntPoint(ulong id, long count) =>
        TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["count"] = count },
        };

    private static Point TitlePoint(ulong id, string title) =>
        TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["title"] = title },
        };
}
