using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

public sealed class GeoGapsTests
{
    [Fact]
    public void GeoPoint_Zero_Passes()
    {
        new GeoPoint { Lon = 0, Lat = 0 }.Validate();
    }

    [Fact]
    public void GeoRadius_Zero_Passes()
    {
        new GeoRadius
        {
            Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
            Radius = 0.0,
        }.Validate();
    }

    [Fact]
    public void GeoRadius_NullCenter_Throws()
    {
        GeoRadius radius = new()
        {
            Center = null!,
            Radius = 10.0,
        };
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(radius.Validate);
        Assert.Equal("Center", ex.ParamName);
    }

    [Fact]
    public void GeoRadius_PosInf_Throws()
    {
        GeoRadius radius = new()
        {
            Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
            Radius = double.PositiveInfinity,
        };
        Assert.Throws<EdgeInvalidArgumentException>(radius.Validate);
    }

    [Fact]
    public void GeoRadius_NegInf_Throws()
    {
        GeoRadius radius = new()
        {
            Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
            Radius = double.NegativeInfinity,
        };
        Assert.Throws<EdgeInvalidArgumentException>(radius.Validate);
    }

    [Fact]
    public void GeoRadius_InvalidCenter_Propagates()
    {
        GeoRadius radius = new()
        {
            Center = new GeoPoint { Lon = 0, Lat = 999 },
            Radius = 10.0,
        };
        Assert.Throws<EdgeInvalidArgumentException>(radius.Validate);
    }

    [Fact]
    public void GeoLineString_NullPoints_Throws()
    {
        GeoLineString line = new() { Points = null! };
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(line.Validate);
        Assert.Equal("Points", ex.ParamName);
    }

    [Fact]
    public void GeoLineString_NullElement_Throws()
    {
        GeoLineString line = new() { Points = [null!] };
        Assert.Throws<ArgumentNullException>(line.Validate);
    }

    [Fact]
    public void GeoLineString_InvalidPoint_Propagates()
    {
        GeoLineString line = new()
        {
            Points =
            [
                new GeoPoint { Lon = 0, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 999 },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(line.Validate);
    }

    [Fact]
    public void GeoLineString_Empty_PassesValidate()
    {
        new GeoLineString { Points = [] }.Validate();
    }

    [Fact]
    public void GeoPolygon_NullExterior_Throws()
    {
        GeoPolygon polygon = new() { Exterior = null! };
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(polygon.Validate);
        Assert.Equal("Exterior", ex.ParamName);
    }

    [Fact]
    public void GeoPolygon_NullInteriorElement_Throws()
    {
        GeoPolygon polygon = new()
        {
            Exterior = ClosedSquare(),
            Interiors = [null!],
        };
        Assert.Throws<ArgumentNullException>(polygon.Validate);
    }

    [Fact]
    public void GeoPolygon_NullInteriors_Passes()
    {
        new GeoPolygon { Exterior = ClosedSquare(), Interiors = null }.Validate();
    }

    [Fact]
    public void GeoPolygon_InteriorUnclosed_Throws()
    {
        GeoPolygon polygon = new()
        {
            Exterior = ClosedSquare(),
            Interiors =
            [
                new GeoLineString
                {
                    Points =
                    [
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.8 },
                        new GeoPoint { Lon = 0.3, Lat = 0.3 },
                    ],
                },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(polygon.Validate);
    }

    [Fact]
    public void GeoPolygon_InteriorTooFew_Throws()
    {
        GeoPolygon polygon = new()
        {
            Exterior = ClosedSquare(),
            Interiors =
            [
                new GeoLineString
                {
                    Points =
                    [
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.2 },
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                    ],
                },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(polygon.Validate);
    }

    [Fact]
    public void GeoPolygon_InteriorInvalidPoint_Throws()
    {
        GeoPolygon polygon = new()
        {
            Exterior = ClosedSquare(),
            Interiors =
            [
                new GeoLineString
                {
                    Points =
                    [
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 999 },
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                    ],
                },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(polygon.Validate);
    }

    [Fact]
    public void GeoPolygon_LonMismatchOnly_Throws()
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
                    new GeoPoint { Lon = 1, Lat = 0 },
                ],
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(polygon.Validate);
    }

    [Fact]
    public void GeoPolygon_ValidHole_PassesValidate()
    {
        GeoPolygon polygon = new()
        {
            Exterior = ClosedSquare(),
            Interiors =
            [
                new GeoLineString
                {
                    Points =
                    [
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.2 },
                        new GeoPoint { Lon = 0.8, Lat = 0.8 },
                        new GeoPoint { Lon = 0.2, Lat = 0.2 },
                    ],
                },
            ],
        };
        polygon.Validate();
    }

    [Fact]
    public void GeoBoundingBox_NullCorner_ThrowsNRE()
    {
        FieldCondition topNull = new()
        {
            Key = "loc",
            GeoBoundingBox = new GeoBoundingBox
            {
                TopLeft = null!,
                BottomRight = new GeoPoint { Lon = 14, Lat = 52 },
            },
        };
        Assert.Throws<NullReferenceException>(topNull.Validate);

        FieldCondition bottomNull = new()
        {
            Key = "loc",
            GeoBoundingBox = new GeoBoundingBox
            {
                TopLeft = new GeoPoint { Lon = 13, Lat = 53 },
                BottomRight = null!,
            },
        };
        Assert.Throws<NullReferenceException>(bottomNull.Validate);
    }

    [Fact]
    public void GeoBoundingBox_NullItself_Skips()
    {
        FieldCondition field = new()
        {
            Key = "loc",
            Match = Match.CreateText("a"),
            GeoBoundingBox = null,
        };
        field.Validate();
    }

    [Fact]
    public void GeoBoundingBox_InvalidCorner_Throws()
    {
        FieldCondition field = new()
        {
            Key = "loc",
            GeoBoundingBox = new GeoBoundingBox
            {
                TopLeft = new GeoPoint { Lon = 13, Lat = 999 },
                BottomRight = new GeoPoint { Lon = 14, Lat = 52 },
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(field.Validate);
    }

    [Fact]
    public void GeoBoundingBoxFilter_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        Filter inside = new()
        {
            Must =
            [
                Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoBoundingBox = new GeoBoundingBox
                    {
                        TopLeft = new GeoPoint { Lon = 13.0, Lat = 53.0 },
                        BottomRight = new GeoPoint { Lon = 14.0, Lat = 52.0 },
                    },
                }),
            ],
        };
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = inside }));

        Filter outside = new()
        {
            Must =
            [
                Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoBoundingBox = new GeoBoundingBox
                    {
                        TopLeft = new GeoPoint { Lon = -1.0, Lat = 1.0 },
                        BottomRight = new GeoPoint { Lon = 1.0, Lat = -1.0 },
                    },
                }),
            ],
        };
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = outside }));
    }

    [Fact]
    public void GeoRadiusFilter_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        Filter near = new()
        {
            Must =
            [
                Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoRadius = new GeoRadius
                    {
                        Center = new GeoPoint { Lon = 13.4, Lat = 52.5 },
                        Radius = 100_000.0,
                    },
                }),
            ],
        };
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = near }));
    }

    [Fact]
    public void GeoPolygonFilter_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        Filter inside = new()
        {
            Must =
            [
                Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoPolygon = new GeoPolygon
                    {
                        Exterior = new GeoLineString
                        {
                            Points =
                            [
                                new GeoPoint { Lon = 13.0, Lat = 52.0 },
                                new GeoPoint { Lon = 14.0, Lat = 52.0 },
                                new GeoPoint { Lon = 14.0, Lat = 53.0 },
                                new GeoPoint { Lon = 13.0, Lat = 53.0 },
                                new GeoPoint { Lon = 13.0, Lat = 52.0 },
                            ],
                        },
                    },
                }),
            ],
        };
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = inside }));
    }

    private static GeoLineString ClosedSquare() => new()
    {
        Points =
        [
            new GeoPoint { Lon = 0, Lat = 0 },
            new GeoPoint { Lon = 1, Lat = 0 },
            new GeoPoint { Lon = 1, Lat = 1 },
            new GeoPoint { Lon = 0, Lat = 0 },
        ],
    };

    private static EdgeShard GeoShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("loc", PayloadSchemaType.Geo));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
                {
                    Payload = new JsonObject { ["loc"] = new JsonObject { ["lon"] = 13.4, ["lat"] = 52.5 } },
                },
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
                {
                    Payload = new JsonObject { ["loc"] = new JsonObject { ["lon"] = 0.0, ["lat"] = 0.0 } },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }
}
