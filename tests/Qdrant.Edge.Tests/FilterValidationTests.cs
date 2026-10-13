namespace Qdrant.Edge.Tests;

public sealed class FilterValidationTests
{
    [Fact]
    public void Validate_FieldConditionWithoutPredicate_ThrowsInvalidArgument()
    {
        Filter filter = new() { Must = [Condition.Field(new FieldCondition { Key = "city" })] };
        Assert.Throws<EdgeInvalidArgumentException>(filter.Validate);
    }

    [Fact]
    public void Validate_FieldConditionWithTwoPredicates_ThrowsInvalidArgument()
    {
        FieldCondition field = new()
        {
            Key = "city",
            Match = Match.CreateText("red"),
            Range = new RangeFloat { Gte = 1.0 },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_SliceIndexBeyondTotal_ThrowsInvalidArgument()
    {
        Filter filter = new() { Must = [Condition.Slice(2, 2)] };
        Assert.Throws<EdgeInvalidArgumentException>(filter.Validate);
    }

    [Fact]
    public void Validate_SliceZeroTotal_ThrowsInvalidArgument()
    {
        Filter filter = new() { Must = [Condition.Slice(0, 0)] };
        Assert.Throws<EdgeInvalidArgumentException>(filter.Validate);
    }

    [Fact]
    public void Validate_UnclosedPolygon_ThrowsInvalidArgument()
    {
        GeoPolygon polygon = new()
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
        FieldCondition field = new() { Key = "geo", GeoPolygon = polygon };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_RangePlusDatetimeRange_ThrowsInvalidArgument()
    {
        FieldCondition field = new()
        {
            Key = "ts",
            Range = new RangeFloat { Gte = 1.0 },
            DatetimeRange = new RangeDatetime { Gte = "2024-01-01T00:00:00Z" },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_NegativeGeoRadius_ThrowsInvalidArgument()
    {
        FieldCondition field = new()
        {
            Key = "geo",
            GeoRadius = new GeoRadius
            {
                Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
                Radius = -1.0,
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_NaNGeoRadius_ThrowsInvalidArgument()
    {
        FieldCondition field = new()
        {
            Key = "geo",
            GeoRadius = new GeoRadius
            {
                Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
                Radius = double.NaN,
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_TooFewPolygonPoints_ThrowsInvalidArgument()
    {
        GeoPolygon polygon = new()
        {
            Exterior = new GeoLineString
            {
                Points =
                [
                    new GeoPoint { Lon = 0, Lat = 0 },
                    new GeoPoint { Lon = 1, Lat = 0 },
                    new GeoPoint { Lon = 0, Lat = 0 },
                ],
            },
        };
        FieldCondition field = new() { Key = "geo", GeoPolygon = polygon };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void Validate_BadPolygonCoordinate_ThrowsInvalidArgument()
    {
        GeoPolygon polygon = new()
        {
            Exterior = new GeoLineString
            {
                Points =
                [
                    new GeoPoint { Lon = 0, Lat = 0 },
                    new GeoPoint { Lon = 1, Lat = 0 },
                    new GeoPoint { Lon = 1, Lat = 999 },
                    new GeoPoint { Lon = 0, Lat = 0 },
                ],
            },
        };
        FieldCondition field = new() { Key = "geo", GeoPolygon = polygon };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }
}
