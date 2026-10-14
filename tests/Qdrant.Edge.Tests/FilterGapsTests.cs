using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class FilterGapsTests
{
    private sealed record FakeCondition : Condition;

    private static FieldCondition ValidMatchField() => new() { Key = "city", Match = Match.CreateText("red") };

    private static GeoPolygon ValidPolygon() => new()
    {
        Exterior = new GeoLineString
        {
            Points =
            [
                new GeoPoint { Lon = 0, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 1 },
                new GeoPoint { Lon = 0, Lat = 0 },
            ],
        },
    };

    private static GeoPolygon UnclosedPolygon() => new()
    {
        Exterior = new GeoLineString
        {
            Points =
            [
                new GeoPoint { Lon = 0, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 1 },
                new GeoPoint { Lon = 0, Lat = 1 },
            ],
        },
    };

    private static Condition RoundTripCondition(Condition original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Condition);
        Condition? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Condition);
        Assert.NotNull(back);
        return back;
    }

    [Fact]
    public void FieldCondition_EmptyKey_ThrowsArgument()
    {
        var field = new FieldCondition { Key = string.Empty, Match = Match.CreateText("red") };
        Assert.Throws<ArgumentException>(field.Validate);
    }

    [Fact]
    public void FieldCondition_NullKey_ThrowsArgumentNull()
    {
        var field = new FieldCondition { Key = null!, Match = Match.CreateText("red") };
        Assert.Throws<ArgumentNullException>(field.Validate);
    }

    [Fact]
    public void FieldCondition_EachSinglePredicate_Passes()
    {
        new FieldCondition { Key = "city", Match = Match.CreateText("red") }.Validate();
        new FieldCondition { Key = "price", Range = new RangeFloat { Gte = 1.0 } }.Validate();
        new FieldCondition { Key = "ts", DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z" } }.Validate();
        new FieldCondition
        {
            Key = "geo",
            GeoBoundingBox = new GeoBoundingBox
            {
                TopLeft = new GeoPoint { Lon = -10, Lat = 10 },
                BottomRight = new GeoPoint { Lon = 10, Lat = -10 },
            },
        }.Validate();
        new FieldCondition
        {
            Key = "geo",
            GeoRadius = new GeoRadius
            {
                Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
                Radius = 1000.0,
            },
        }.Validate();
        new FieldCondition { Key = "geo", GeoPolygon = ValidPolygon() }.Validate();
        new FieldCondition { Key = "tags", ValuesCount = new ValuesCount { Gte = 1 } }.Validate();
    }

    [Fact]
    public void FieldCondition_RangeCountsAsOne_Passes()
    {
        new FieldCondition { Key = "price", Range = new RangeFloat { Gte = 1.0 } }.Validate();
    }

    [Fact]
    public void FieldCondition_DatetimeRangeAlone_Passes()
    {
        new FieldCondition { Key = "ts", DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z" } }.Validate();
    }

    [Fact]
    public void FieldCondition_ThreePredicates_Throws()
    {
        var field = new FieldCondition
        {
            Key = "city",
            Match = Match.CreateText("red"),
            Range = new RangeFloat { Gte = 1.0 },
            ValuesCount = new ValuesCount { Gte = 1 },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void FieldCondition_InvalidGeoRadius_Propagates()
    {
        var field = new FieldCondition
        {
            Key = "geo",
            Match = Match.CreateText("x"),
            GeoRadius = new GeoRadius
            {
                Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
                Radius = -1.0,
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void FieldCondition_InvalidPolygon_Propagates()
    {
        var field = new FieldCondition
        {
            Key = "geo",
            Match = Match.CreateText("x"),
            GeoPolygon = UnclosedPolygon(),
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void FieldCondition_InvalidBoundingBoxCorner_Propagates()
    {
        var field = new FieldCondition
        {
            Key = "geo",
            Match = Match.CreateText("x"),
            GeoBoundingBox = new GeoBoundingBox
            {
                TopLeft = new GeoPoint { Lon = 0, Lat = 999 },
                BottomRight = new GeoPoint { Lon = 10, Lat = 10 },
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void ConditionDepth_EachLeaf_ReturnsZero()
    {
        Assert.Equal(0u, Condition.Field(ValidMatchField()).Depth());
        Assert.Equal(0u, Condition.IsEmpty("k").Depth());
        Assert.Equal(0u, Condition.IsNull("k").Depth());
        Assert.Equal(0u, Condition.HasId([PointId.FromNumber(1)]).Depth());
        Assert.Equal(0u, Condition.HasVector("vec").Depth());
        Assert.Equal(0u, Condition.Slice(1, 0).Depth());
    }

    [Fact]
    public void ConditionDepth_UnknownShape_ReturnsZero()
    {
        Condition forged = new FakeCondition();
        Assert.Equal(0u, forged.Depth());
    }

    [Fact]
    public void ConditionDepth_Nested_AddsOne()
    {
        Assert.Equal(1u, Condition.Nested("k", new Filter()).Depth());
    }

    [Fact]
    public void ConditionDepth_Filter_AddsOne()
    {
        Assert.Equal(1u, Condition.NestedFilter(new Filter()).Depth());
    }

    [Fact]
    public void ConditionDepth_DoubleNested_AddsTwo()
    {
        Condition inner = Condition.Nested("b", new Filter());
        Condition outer = Condition.Nested("a", new Filter { Must = [inner] });
        Assert.Equal(2u, outer.Depth());
    }

    [Fact]
    public void ConditionValidate_IsEmptyNullKey_ThrowsArgumentNull()
    {
        Condition condition = Condition.IsEmpty(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_IsNullNullKey_ThrowsArgumentNull()
    {
        Condition condition = Condition.IsNull(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_HasIdNull_Throws()
    {
        Condition condition = Condition.HasId(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_HasIdEmpty_Passes()
    {
        Condition.HasId([]).Validate();
    }

    [Fact]
    public void ConditionValidate_HasVectorNull_ThrowsArgumentNull()
    {
        Condition condition = Condition.HasVector(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_HasVectorWhitespace_ThrowsArgument()
    {
        Condition condition = Condition.HasVector("  ");
        Assert.Throws<ArgumentException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_HasVectorEmpty_ThrowsArgument()
    {
        Condition condition = Condition.HasVector(string.Empty);
        Assert.Throws<ArgumentException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_SliceBoundaries()
    {
        Condition.Slice(1, 0).Validate();
        Condition.Slice(2, 1).Validate();
    }

    [Fact]
    public void ConditionValidate_NestedNullKey_Throws()
    {
        Condition condition = Condition.Nested(null!, new Filter());
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_NestedNullFilter_Throws()
    {
        Condition condition = Condition.Nested("k", null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_NestedInvalidFilter_Propagates()
    {
        Condition condition = Condition.Nested("k", new Filter { Must = [Condition.Slice(0, 0)] });
        Assert.Throws<EdgeInvalidArgumentException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_FilterConditionNull_Throws()
    {
        Condition condition = Condition.NestedFilter(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_FilterConditionInvalid_Propagates()
    {
        Condition condition = Condition.NestedFilter(new Filter { Must = [Condition.Slice(0, 0)] });
        Assert.Throws<EdgeInvalidArgumentException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_FieldNull_Throws()
    {
        Condition condition = Condition.Field(null!);
        Assert.Throws<ArgumentNullException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_FieldInvalid_Propagates()
    {
        Condition condition = Condition.Field(new FieldCondition { Key = "city" });
        Assert.Throws<EdgeInvalidArgumentException>(condition.Validate);
    }

    [Fact]
    public void ConditionValidate_UnknownShape_Throws()
    {
        Condition forged = new FakeCondition();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void Condition_Kinds_RoundTripThroughJson()
    {
        Condition field = Condition.Field(ValidMatchField());
        Condition fieldBack = RoundTripCondition(field);
        Assert.IsType<FieldConditionClause>(fieldBack);
        Assert.Equal(field, fieldBack);

        Condition isEmpty = Condition.IsEmpty("k");
        Assert.IsType<IsEmptyCondition>(RoundTripCondition(isEmpty));

        Condition isNull = Condition.IsNull("k");
        Assert.IsType<IsNullCondition>(RoundTripCondition(isNull));

        Condition hasId = Condition.HasId([PointId.FromNumber(1)]);
        Assert.IsType<HasIdCondition>(RoundTripCondition(hasId));

        Condition hasVector = Condition.HasVector("vec");
        Assert.IsType<HasVectorCondition>(RoundTripCondition(hasVector));

        Condition slice = Condition.Slice(2, 1);
        Assert.IsType<SliceCondition>(RoundTripCondition(slice));

        Condition nested = Condition.Nested("k", new Filter());
        Assert.IsType<NestedCondition>(RoundTripCondition(nested));

        Condition filter = Condition.NestedFilter(new Filter());
        Assert.IsType<FilterCondition>(RoundTripCondition(filter));
    }

    [Fact]
    public void FilterDepth_Empty_ReturnsZero()
    {
        Assert.Equal(0u, new Filter().Depth());
    }

    [Fact]
    public void FilterDepth_MaxBranch_Wins()
    {
        Condition level1 = Condition.Nested("k", new Filter());
        Condition level2 = Condition.Nested("k", new Filter { Must = [level1] });
        Condition level3 = Condition.Nested("k", new Filter { Must = [level2] });
        var filter = new Filter
        {
            Must = [Condition.IsEmpty("k")],
            Should = [level3],
        };
        Assert.Equal(3u, filter.Depth());
    }

    [Fact]
    public void FilterDepth_MinShould_Contributes()
    {
        Condition level1 = Condition.Nested("k", new Filter());
        Condition level2 = Condition.Nested("k", new Filter { Must = [level1] });
        var filter = new Filter
        {
            MinShould = new MinShould { Conditions = [level2], MinCount = 1 },
        };
        Assert.Equal(2u, filter.Depth());
    }

    [Fact]
    public void FilterValidate_NullElement_Throws()
    {
        var filter = new Filter { Must = [null!] };
        Assert.Throws<NullReferenceException>(filter.Validate);
    }

    [Fact]
    public void FilterValidate_MinShouldNullConditions_ThrowsArgumentNull()
    {
        var filter = new Filter { MinShould = new MinShould { Conditions = null!, MinCount = 1 } };
        Assert.Throws<ArgumentNullException>(filter.Validate);
    }

    [Fact]
    public void FilterValidate_MinShouldEmpty_Passes()
    {
        new Filter { MinShould = new MinShould { Conditions = [], MinCount = 1 } }.Validate();
    }

    [Fact]
    public void FilterValidate_InvalidBranch_Propagates()
    {
        Condition valid = Condition.IsEmpty("k");
        Condition invalid = Condition.Slice(0, 0);
        Assert.Throws<EdgeInvalidArgumentException>(new Filter { Must = [valid], Should = [invalid] }.Validate);
        Assert.Throws<EdgeInvalidArgumentException>(new Filter { Must = [valid], MustNot = [invalid] }.Validate);
        Assert.Throws<EdgeInvalidArgumentException>(new Filter
        {
            Must = [valid],
            MinShould = new MinShould { Conditions = [invalid], MinCount = 1 },
        }.Validate);
    }

    [Fact]
    public void MinShould_MinCountZero_Passes()
    {
        var filter = new Filter
        {
            MinShould = new MinShould
            {
                Conditions = [Condition.Field(ValidMatchField())],
                MinCount = 0,
            },
        };
        filter.Validate();

        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f),
            ]));
            ulong count = shard.Count(new CountRequest { Filter = filter });
            Assert.Equal(3ul, count);
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }
}
