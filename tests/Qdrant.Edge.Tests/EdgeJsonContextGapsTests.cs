using System.Text.Json;

namespace Qdrant.Edge.Tests;

/// <summary>Source-generated context completeness pins for every <c>EdgeJsonContext</c> entry (§18).
/// Each entry gets a serialize+deserialize round-trip through <c>EdgeJsonContext.Deep</c>: a missing
/// entry surfaces only at runtime as <c>InvalidOperationException</c>, so this file is the checklist.
/// Entries already round-tripped elsewhere carry a smoke pin citing the primary file.</summary>
public sealed class EdgeJsonContextGapsTests
{
    private static NamedVector Dense4() => NamedVector.Dense([1f, 2f, 3f, 4f]);

    private static Filter CityFilter() => new() { Must = [Condition.IsEmpty("city")] };

    private static Point TestPoint(ulong id = 1) =>
        new() { Id = PointId.FromNumber(id), Vector = Vector.Single([1f, 2f]) };

    private static ScoredPoint TestScored(ulong id = 1) =>
        new() { Id = PointId.FromNumber(id), Version = 3, Score = 0.5f };

    // Config entries (no Deep round-trip anywhere else).

    [Fact]
    public void EdgeConfig_DeepRoundTrip_PreservesVectorData()
    {
        EdgeConfig original = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Cosine },
            },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.EdgeConfig);
        EdgeConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.EdgeConfig);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.EdgeConfig));
        Assert.Equal(4ul, back.VectorData[""].Size);
        Assert.Equal(Distance.Cosine, back.VectorData[""].Distance);
    }

    [Fact]
    public void VectorDataConfig_DeepRoundTrip_PreservesSizeAndHnsw()
    {
        VectorDataConfig original = new()
        {
            Size = 4,
            Distance = Distance.Dot,
            HnswConfig = new HnswIndexConfig { M = 32, EfConstruct = 200 },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.VectorDataConfig);
        VectorDataConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VectorDataConfig);
        Assert.Equal(original, back);
    }

    [Fact]
    public void HnswIndexConfig_DeepRoundTrip_PreservesBounds()
    {
        HnswIndexConfig original = new() { M = 32, EfConstruct = 200, MaxIndexingThreads = 8 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.HnswIndexConfig);
        HnswIndexConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.HnswIndexConfig);
        Assert.Equal(original, back);
    }

    [Fact]
    public void OptimizersConfig_DeepRoundTrip_PreservesThresholds()
    {
        OptimizersConfig original = new() { DeletedThreshold = 0.25, VacuumMinVectorNumber = 1000 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.OptimizersConfig);
        OptimizersConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.OptimizersConfig);
        Assert.Equal(original, back);
    }

    [Fact]
    public void SparseVectorDataConfig_DeepSmoke_RoundTrips()
    {
        // Primary pins live in ConfigGapsTests.cs; this is the §18 checklist smoke.
        SparseVectorDataConfig original = new() { FullScanThreshold = 500, Modifier = Modifier.Idf };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SparseVectorDataConfig);
        SparseVectorDataConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SparseVectorDataConfig);
        Assert.Equal(original, back);
    }

    // Point id entries.

    [Fact]
    public void NumPointId_DeepRoundTrip_PreservesValue()
    {
        NumPointId original = new(7);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.NumPointId);
        NumPointId? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.NumPointId);
        Assert.Equal(original, back);
    }

    [Fact]
    public void UuidPointId_DeepRoundTrip_PreservesValue()
    {
        UuidPointId original = new(new Guid("11111111-2222-3333-4444-555555555555"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UuidPointId);
        UuidPointId? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UuidPointId);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(1ul)]
    [InlineData(ulong.MaxValue)]
    public void PointId_DeepSmoke_RoundTripsBothKinds(ulong value)
    {
        // Primary pins live in PointIdGapsTests.cs; this is the §18 checklist smoke.
        PointId original = PointId.FromNumber(value);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PointId);
        PointId? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PointId);
        Assert.Equal(original, back);
        Assert.IsType<NumPointId>(back);
    }

    // Points entries.

    [Fact]
    public void Point_DeepRoundTrip_PreservesId()
    {
        Point original = TestPoint();
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Point);
        Point? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Point);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Point));
        Assert.Equal(PointId.FromNumber(1), back.Id);
    }

    [Fact]
    public void PointVectors_DeepRoundTrip_PreservesId()
    {
        PointVectors original = new() { Id = PointId.FromNumber(9), Vector = Vector.Single([1f]) };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PointVectors);
        PointVectors? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PointVectors);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.PointVectors));
        Assert.Equal(PointId.FromNumber(9), back.Id);
    }

    public static IEnumerable<object[]> OrderValueCases()
    {
        yield return [OrderValue.Int(42)];
        yield return [OrderValue.Float(1.5)];
    }

    [Theory]
    [MemberData(nameof(OrderValueCases))]
    public void OrderValue_DeepSmoke_RoundTripsBothArms(OrderValue original)
    {
        // Primary pins live in PointsGapsTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.OrderValue);
        OrderValue? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.OrderValue);
        Assert.Equal(original, back);
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void ScoredPoint_DeepSmoke_PreservesAllOptionals()
    {
        // Primary pins live in PointsGapsTests.cs; this exercises the all-set shape.
        ScoredPoint original = TestScored() with
        {
            Payload = """{"city":"Oslo"}""",
            Vector = "[1,2]",
            OrderValue = OrderValue.Int(3),
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoredPoint);
        ScoredPoint? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoredPoint);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ScoredPointArray_DeepRoundTrip_PreservesLength()
    {
        ScoredPoint[] original = [TestScored(1), TestScored(2)];
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoredPointArray);
        ScoredPoint[]? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoredPointArray);
        Assert.NotNull(back);
        Assert.Equal(2, back.Length);
        Assert.Equal(PointId.FromNumber(2), back[1].Id);
    }

    [Fact]
    public void ScoredPointArrayArray_DeepRoundTrip_PreservesNesting()
    {
        ScoredPoint[][] original = [[TestScored(1)], [TestScored(2), TestScored(3)]];
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoredPointArrayArray);
        ScoredPoint[][]? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoredPointArrayArray);
        Assert.NotNull(back);
        Assert.Equal(2, back.Length);
        Assert.Equal(2, back[1].Length);
        Assert.Equal(PointId.FromNumber(3), back[1][1].Id);
    }

    [Fact]
    public void Record_DeepSmoke_PreservesAllOptionals()
    {
        // Primary pins live in PointsGapsTests.cs; this exercises the all-set shape.
        Record original = new()
        {
            Id = PointId.FromNumber(4),
            Payload = """{"city":"Oslo"}""",
            Vector = "[1,2]",
            OrderValue = OrderValue.Float(2.5),
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Record);
        Record? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Record);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RecordArray_DeepRoundTrip_PreservesLength()
    {
        Record[] original = [new Record { Id = PointId.FromNumber(1) }, new Record { Id = PointId.FromNumber(2) }];
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RecordArray);
        Record[]? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RecordArray);
        Assert.NotNull(back);
        Assert.Equal(2, back.Length);
        Assert.Equal(PointId.FromNumber(1), back[0].Id);
    }

    // Filter entries.

    [Fact]
    public void Filter_DeepRoundTrip_PreservesBranches()
    {
        Filter original = new()
        {
            Must = [Condition.IsEmpty("city")],
            Should = [Condition.IsNull("zip")],
            MinShould = new MinShould { Conditions = [Condition.HasVector("image")], MinCount = 1 },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Filter);
        Filter? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Filter);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Filter));
        Assert.Single(back.Must!);
        Assert.Single(back.Should!);
        Assert.Equal(1ul, back.MinShould!.MinCount);
    }

    [Fact]
    public void FieldCondition_DeepRoundTrip_PreservesKeyAndRange()
    {
        FieldCondition original = new() { Key = "age", Range = new RangeFloat { Gte = 3, Lt = 10 } };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.FieldCondition);
        FieldCondition? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.FieldCondition);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> ValueVariantCases()
    {
        yield return [ValueVariants.String("Oslo")];
        yield return [ValueVariants.Integer(42)];
        yield return [ValueVariants.Bool(true)];
    }

    [Theory]
    [MemberData(nameof(ValueVariantCases))]
    public void ValueVariants_DeepRoundTrip_PreservesArm(ValueVariants original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ValueVariants);
        ValueVariants? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ValueVariants);
        Assert.Equal(original, back);
        Assert.IsType(original.GetType(), back);
    }

    public static IEnumerable<object[]> AnyVariantCases()
    {
        yield return [AnyVariants.Strings(["a", "b"])];
        yield return [AnyVariants.Integers([1, 2])];
    }

    [Theory]
    [MemberData(nameof(AnyVariantCases))]
    public void AnyVariants_DeepRoundTrip_PreservesArm(AnyVariants original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.AnyVariants);
        AnyVariants? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.AnyVariants);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.AnyVariants));
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void MinShould_DeepRoundTrip_PreservesMinCount()
    {
        MinShould original = new() { Conditions = [Condition.IsEmpty("a"), Condition.IsNull("b")], MinCount = 2 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MinShould);
        MinShould? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MinShould);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.MinShould));
        Assert.Equal(2ul, back.MinCount);
        Assert.Equal(2, back.Conditions.Count);
    }

    [Fact]
    public void Condition_DeepSmoke_RoundTripsLeaf()
    {
        // Primary pins live in FilterGapsTests.cs; this is the §18 checklist smoke.
        Condition original = Condition.IsEmpty("city");
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Condition);
        Condition? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Condition);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Match_DeepSmoke_RoundTripsText()
    {
        // Primary pins live in MatchGapsTests.cs; this is the §18 checklist smoke.
        Match original = Match.CreateText("hello");
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Match);
        Match? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Match);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeFloat_DeepSmoke_PreservesAllBounds()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        RangeFloat original = new() { Gte = 1.5, Gt = 2.5, Lte = 8.5, Lt = 9.5 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeFloat);
        RangeFloat? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeFloat);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeDatetime_DeepSmoke_PreservesAllBounds()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        RangeDatetime original = new() { Gte = "2024-01-01T00:00:00Z", Lt = "2025-01-01T00:00:00Z" };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeDatetime);
        RangeDatetime? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeDatetime);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ValuesCount_DeepSmoke_PreservesAllBounds()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        ValuesCount original = new() { Gte = 1, Lt = 5 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ValuesCount);
        ValuesCount? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ValuesCount);
        Assert.Equal(original, back);
    }

    // Geo entries (no Deep round-trip anywhere else).

    [Fact]
    public void GeoPoint_DeepRoundTrip_PreservesCoordinates()
    {
        GeoPoint original = new() { Lon = 12.5, Lat = -3.25 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoPoint);
        GeoPoint? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoPoint);
        Assert.Equal(original, back);
    }

    [Fact]
    public void GeoBoundingBox_DeepRoundTrip_PreservesCorners()
    {
        GeoBoundingBox original = new()
        {
            TopLeft = new GeoPoint { Lon = -1, Lat = 2 },
            BottomRight = new GeoPoint { Lon = 3, Lat = -4 },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoBoundingBox);
        GeoBoundingBox? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoBoundingBox);
        Assert.Equal(original, back);
    }

    [Fact]
    public void GeoRadius_DeepRoundTrip_PreservesRadius()
    {
        GeoRadius original = new() { Center = new GeoPoint { Lon = 0, Lat = 0 }, Radius = 100 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoRadius);
        GeoRadius? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoRadius);
        Assert.Equal(original, back);
    }

    [Fact]
    public void GeoLineString_DeepRoundTrip_PreservesPoints()
    {
        GeoLineString original = new()
        {
            Points = [new GeoPoint { Lon = 0, Lat = 0 }, new GeoPoint { Lon = 1, Lat = 1 }],
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoLineString);
        GeoLineString? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoLineString);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.GeoLineString));
        Assert.Equal(2, back.Points.Count);
    }

    [Fact]
    public void GeoPolygon_DeepRoundTrip_PreservesExterior()
    {
        GeoPolygon original = new()
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
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoPolygon);
        GeoPolygon? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoPolygon);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.GeoPolygon));
        Assert.Equal(4, back.Exterior.Points.Count);
    }

    // Query / scoring entries.

    public static IEnumerable<object[]> FusionCases()
    {
        yield return [Fusion.Rrf(2)];
        yield return [Fusion.Rrf(3, [1f, 2f])];
        yield return [Fusion.Dbsf()];
    }

    [Theory]
    [MemberData(nameof(FusionCases))]
    public void Fusion_DeepRoundTrip_PreservesArm(Fusion original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Fusion);
        Fusion? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Fusion);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Fusion));
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void OrderBy_DeepRoundTrip_PreservesKeyAndDirection()
    {
        OrderBy original = new()
        {
            Key = "rank",
            Direction = Direction.Desc,
            StartFrom = new IntegerStartFrom(3),
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.OrderBy);
        OrderBy? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.OrderBy);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> StartFromCases()
    {
        yield return [new IntegerStartFrom(3)];
        yield return [new FloatStartFrom(1.5)];
        yield return [new DatetimeStartFrom("2024-01-02T03:04:05Z")];
    }

    [Theory]
    [MemberData(nameof(StartFromCases))]
    public void StartFrom_DeepRoundTrip_PreservesArm(StartFrom original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.StartFrom);
        StartFrom? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.StartFrom);
        Assert.Equal(original, back);
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void Query_DeepSmoke_RoundTripsNearest()
    {
        // Primary pins live in QueryGapsTests.cs; this is the §18 checklist smoke.
        Query original = Query.Nearest(Dense4());
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Query);
        Query? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Query);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Query));
        Assert.IsType<NearestQuery>(back);
    }

    public static IEnumerable<object[]> ScoringQuerySmokeCases()
    {
        yield return [ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f])))];
        yield return [ScoringQuery.CreateFusion(Fusion.Dbsf())];
        yield return [ScoringQuery.CreateSample(Sample.Random)];
    }

    [Theory]
    [MemberData(nameof(ScoringQuerySmokeCases))]
    public void ScoringQuery_DeepSmoke_RoundTripsArm(ScoringQuery original)
    {
        // Primary pins live in ScoringQueryGapsTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScoringQuery));
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void SearchParams_DeepSmoke_PreservesAllFields()
    {
        // Primary pins live in ScoringQueryGapsTests.cs; this is the §18 checklist smoke.
        SearchParams original = new() { HnswEf = 16, Exact = true, IndexedOnly = true };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SearchParams);
        SearchParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchParams);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Prefetch_DeepRoundTrip_PreservesLimitAndBranches()
    {
        Prefetch original = new()
        {
            Limit = 7,
            Query = ScoringQuery.CreateVector(Query.Nearest(Dense4())),
            Filter = CityFilter(),
            ScoreThreshold = 0.5f,
            Params = new SearchParams { Exact = true },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Prefetch);
        Prefetch? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Prefetch);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Prefetch));
        Assert.Equal(7ul, back.Limit);
        Assert.NotNull(back.Query);
        Assert.NotNull(back.Filter);
    }

    // Request / response entries.

    [Fact]
    public void QueryRequest_DeepSmoke_RoundTripsWithPrefetch()
    {
        // AuditJsonDepthTests.cs pins Deep serialization only; this adds the deserialize half.
        QueryRequest original = new()
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(Dense4())),
            Prefetches = [new Prefetch { Limit = 5 }],
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.QueryRequest);
        QueryRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.QueryRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.QueryRequest));
        Assert.Equal(10ul, back.Limit);
        Assert.Single(back.Prefetches);
    }

    [Fact]
    public void QueryBatchRequest_DeepRoundTrip_PreservesOrder()
    {
        QueryBatchRequest original = new()
        {
            Queries = [new QueryRequest { Limit = 1 }, new QueryRequest { Limit = 2 }],
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.QueryBatchRequest);
        QueryBatchRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.QueryBatchRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.QueryBatchRequest));
        Assert.Equal(2, back.Queries.Count);
        Assert.Equal(2ul, back.Queries[1].Limit);
    }

    [Fact]
    public void SearchRequest_DeepRoundTrip_PreservesLimit()
    {
        SearchRequest original = new()
        {
            Query = Query.Nearest(Dense4()),
            Limit = 5,
            Offset = 1,
            Filter = CityFilter(),
            WithVector = WithVector.Bool(true),
            WithPayload = WithPayload.Bool(false),
            ScoreThreshold = 0.25f,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SearchRequest);
        SearchRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SearchRequest));
        Assert.Equal(5ul, back.Limit);
        Assert.Equal(1ul, back.Offset);
    }

    [Fact]
    public void ScrollRequest_DeepRoundTrip_PreservesOffset()
    {
        ScrollRequest original = new()
        {
            Offset = PointId.FromNumber(2),
            Limit = 5,
            Filter = CityFilter(),
            OrderBy = new OrderBy { Key = "rank" },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScrollRequest);
        ScrollRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScrollRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScrollRequest));
        Assert.Equal(PointId.FromNumber(2), back.Offset);
        Assert.Equal(5ul, back.Limit);
        Assert.Equal("rank", back.OrderBy!.Key);
    }

    [Fact]
    public void ScrollResponse_DeepRoundTrip_PreservesNextOffset()
    {
        ScrollResponse original = new()
        {
            Records = [new Record { Id = PointId.FromNumber(1) }],
            NextOffset = PointId.FromNumber(2),
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScrollResponse);
        ScrollResponse? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScrollResponse);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ScrollResponse));
        Assert.Equal(PointId.FromNumber(2), back.NextOffset);
    }

    [Fact]
    public void RetrieveRequest_DeepRoundTrip_PreservesIds()
    {
        RetrieveRequest original = new()
        {
            PointIds = [PointId.FromNumber(1), PointId.FromNumber(2)],
            WithPayload = WithPayload.Bool(true),
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RetrieveRequest);
        RetrieveRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RetrieveRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.RetrieveRequest));
        Assert.Equal(2, back.PointIds.Count);
    }

    [Fact]
    public void CountRequest_DeepRoundTrip_PreservesExact()
    {
        CountRequest original = new() { Filter = CityFilter(), Exact = false };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CountRequest);
        CountRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CountRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.CountRequest));
        Assert.False(back.Exact);
    }

    [Fact]
    public void FacetRequest_DeepRoundTrip_PreservesKey()
    {
        FacetRequest original = new() { Key = "city", Limit = 5, Exact = true, Filter = CityFilter() };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.FacetRequest);
        FacetRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.FacetRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.FacetRequest));
        Assert.Equal("city", back.Key);
        Assert.True(back.Exact);
    }

    [Fact]
    public void FacetHit_DeepRoundTrip_PreservesValueAndCount()
    {
        FacetHit original = new() { Value = "Oslo", Count = 4 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.FacetHit);
        FacetHit? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.FacetHit);
        Assert.Equal(original, back);
    }

    [Fact]
    public void FacetResponse_DeepSmoke_PreservesTwoHits()
    {
        // Primary pins live in RequestsGapsTests.cs; this exercises the multi-hit shape.
        FacetResponse original = new()
        {
            Hits = [new FacetHit { Value = "a", Count = 1 }, new FacetHit { Value = "b", Count = 2 }],
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.FacetResponse);
        FacetResponse? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.FacetResponse);
        Assert.NotNull(back);
        Assert.Equal(2, back.Count);
        Assert.Equal("a", back[0].Value);
    }

    [Fact]
    public void GroupRequest_DeepRoundTrip_PreservesGroupBy()
    {
        GroupRequest original = new()
        {
            Query = new QueryRequest { Limit = 5 },
            GroupBy = "city",
            Groups = 3,
            GroupSize = 2,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GroupRequest);
        GroupRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GroupRequest);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.GroupRequest));
        Assert.Equal("city", back.GroupBy);
        Assert.Equal(3ul, back.Groups);
    }

    [Fact]
    public void Group_DeepRoundTrip_PreservesKeyAndHits()
    {
        Group original = new() { Key = new StringGroupId("a"), Hits = [TestScored(1)] };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Group);
        Group? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Group);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Group));
        Assert.Equal(new StringGroupId("a"), back.Key);
        Assert.Single(back.Hits);
    }

    [Fact]
    public void GroupArray_DeepRoundTrip_PreservesLength()
    {
        Group[] original =
        [
            new Group { Key = new StringGroupId("a"), Hits = [TestScored(1)] },
            new Group { Key = new UInt64GroupId(7), Hits = [] },
        ];
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GroupArray);
        Group[]? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GroupArray);
        Assert.NotNull(back);
        Assert.Equal(2, back.Length);
        Assert.Equal(new UInt64GroupId(7), back[1].Key);
    }

    public static IEnumerable<object[]> GroupIdSmokeCases()
    {
        yield return [new StringGroupId("a")];
        yield return [new UInt64GroupId(7)];
        yield return [new Int64GroupId(-3)];
    }

    [Theory]
    [MemberData(nameof(GroupIdSmokeCases))]
    public void GroupId_DeepSmoke_RoundTripsAllKinds(GroupId original)
    {
        // Primary pins live in RequestsGapsTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GroupId);
        GroupId? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GroupId);
        Assert.Equal(original, back);
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void SearchMatrixRequest_DeepSmoke_PreservesUsing()
    {
        // Primary pins live in RequestsGapsTests.cs; this is the §18 checklist smoke.
        SearchMatrixRequest original = new() { SampleSize = 2, LimitPerSample = 1, Using = "image" };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SearchMatrixRequest);
        SearchMatrixRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchMatrixRequest);
        Assert.NotNull(back);
        Assert.Equal("image", back.Using);
    }

    [Fact]
    public void SearchMatrixResponse_DeepSmoke_PreservesNearests()
    {
        // Primary pins live in RequestsGapsTests.cs (empty shape); this pins a non-empty one.
        SearchMatrixResponse original = new()
        {
            SampleIds = [PointId.FromNumber(1)],
            Nearests = [[TestScored(2)]],
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SearchMatrixResponse);
        SearchMatrixResponse? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchMatrixResponse);
        Assert.NotNull(back);
        Assert.Single(back.SampleIds);
        Assert.Single(back.Nearests);
        Assert.Equal(PointId.FromNumber(2), back.Nearests[0][0].Id);
    }

    [Fact]
    public void ShardInfo_DeepSmoke_PreservesSchemaEntry()
    {
        // Primary pins live in RequestsGapsTests.cs (empty schema); this pins a non-empty one.
        ShardInfo original = new()
        {
            SegmentsCount = 1,
            PointsCount = 2,
            IndexedVectorsCount = 2,
            PayloadSchema = new Dictionary<string, PayloadIndexInfo>
            {
                ["city"] = new PayloadIndexInfo { DataType = PayloadSchemaType.Keyword, Points = 2 },
            },
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ShardInfo);
        ShardInfo? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ShardInfo);
        Assert.NotNull(back);
        Assert.Equal(1ul, back.SegmentsCount);
        Assert.Equal(PayloadSchemaType.Keyword, back.PayloadSchema["city"].DataType);
    }

    [Fact]
    public void PayloadIndexInfo_DeepSmoke_PreservesParamsArm()
    {
        // Primary pins live in RequestsGapsTests.cs; this is the §18 checklist smoke.
        PayloadIndexInfo original = new()
        {
            DataType = PayloadSchemaType.Keyword,
            Params = PayloadIndexParams.Keyword(new KeywordIndexParams()),
            Points = 7,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PayloadIndexInfo);
        PayloadIndexInfo? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexInfo);
        Assert.NotNull(back);
        Assert.Equal(PayloadSchemaType.Keyword, back.DataType);
        Assert.IsType<KeywordPayloadIndex>(back.Params);
    }

    // Vector / selector / quantization smokes (primary pins cited inline).

    [Fact]
    public void Vector_DeepSmoke_RoundTripsSingle()
    {
        // Primary pins live in VectorGapsTests.cs; this is the §18 checklist smoke.
        Vector original = Vector.Single([1f, 2f]);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Vector);
        Vector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Vector);
        Assert.IsType<SingleVector>(back);
    }

    [Fact]
    public void SparseVector_DeepSmoke_RoundTrips()
    {
        // Primary pins live in VectorGapsTests.cs; this is the §18 checklist smoke.
        SparseVector original = new() { Indices = [1, 5], Values = [0.5f, 1.5f] };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SparseVector);
        SparseVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SparseVector);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SparseVector));
        Assert.Equal(2, back.Indices.Count);
        Assert.Equal(1u, back.Indices[0]);
    }

    public static IEnumerable<object[]> NamedVectorCases()
    {
        yield return [NamedVector.Dense([1f, 2f])];
        yield return [NamedVector.Sparse(new SparseVector { Indices = [1], Values = [0.5f] })];
        yield return [NamedVector.MultiDense([[1f, 2f], [3f, 4f]])];
    }

    [Theory]
    [MemberData(nameof(NamedVectorCases))]
    public void NamedVector_DeepRoundTrip_PreservesArm(NamedVector original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.NamedVector);
        NamedVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.NamedVector);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.NamedVector));
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void WithPayload_DeepSmoke_RoundTripsBool()
    {
        // Primary pins live in PointsGapsTests.cs; this is the §18 checklist smoke.
        WithPayload original = WithPayload.Bool(true);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.WithPayload);
        WithPayload? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithPayload);
        Assert.Equal(original, back);
    }

    [Fact]
    public void WithVector_DeepSmoke_RoundTripsBool()
    {
        // Primary pins live in PointsGapsTests.cs; this is the §18 checklist smoke.
        WithVector original = WithVector.Bool(false);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.WithVector);
        WithVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.WithVector);
        Assert.Equal(original, back);
    }

    [Fact]
    public void QuantizationConfig_DeepSmoke_RoundTripsScalar()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        QuantizationConfig original = QuantizationConfig.Scalar(new ScalarQuantizationParams { Type = ScalarType.Int8 });
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.QuantizationConfig);
        QuantizationConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.QuantizationConfig);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.QuantizationConfig));
        Assert.IsType<ScalarQuantization>(back);
    }

    [Fact]
    public void ScalarQuantizationParams_DeepSmoke_RoundTrips()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        ScalarQuantizationParams original = new() { Type = ScalarType.Int8, Quantile = 0.75f };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScalarQuantizationParams);
        ScalarQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScalarQuantizationParams);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ProductQuantizationParams_DeepSmoke_RoundTrips()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        ProductQuantizationParams original = new() { Compression = CompressionRatio.X16 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ProductQuantizationParams);
        ProductQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ProductQuantizationParams);
        Assert.Equal(original, back);
    }

    [Fact]
    public void BinaryQuantizationParams_DeepSmoke_RoundTrips()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        BinaryQuantizationParams original = new()
        {
            Encoding = BinaryQuantizationEncoding.TwoBits,
            QueryEncoding = BinaryQuantizationQueryEncoding.Scalar8Bits,
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.BinaryQuantizationParams);
        BinaryQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.BinaryQuantizationParams);
        Assert.Equal(original, back);
    }

    [Fact]
    public void TurboQuantizationParams_DeepSmoke_RoundTrips()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        TurboQuantizationParams original = new() { Bits = TurboQuantBitSize.Bits2 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.TurboQuantizationParams);
        TurboQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.TurboQuantizationParams);
        Assert.Equal(original, back);
    }

    [Fact]
    public void PayloadIndexParams_DeepSmoke_RoundTripsKeyword()
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        PayloadIndexParams original = PayloadIndexParams.Keyword(new KeywordIndexParams());
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PayloadIndexParams);
        PayloadIndexParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexParams);
        Assert.IsType<KeywordPayloadIndex>(back);
    }

    // UpdateOperation entries: base + all 20 concretes.

    [Fact]
    public void UpdateOperation_DeepRoundTrip_PreservesConcreteShape()
    {
        // Primary kind pins via the default serializer live in UpdateOperationGapsTests.cs;
        // this pins the Deep base entry.
        UpdateOperation original = UpdateOperation.DeletePoints([PointId.FromNumber(1)]);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UpdateOperation);
        UpdateOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UpdateOperation);
        DeletePointsOperation typed = Assert.IsType<DeletePointsOperation>(back);
        Assert.Single(typed.PointIds);
    }

    [Fact]
    public void UpsertPointsOperation_DeepRoundTrip_PreservesMode()
    {
        UpsertPointsOperation original = Assert.IsType<UpsertPointsOperation>(
            UpdateOperation.UpsertPoints([TestPoint()], null, UpdateMode.InsertOnly));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UpsertPointsOperation);
        UpsertPointsOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UpsertPointsOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.UpsertPointsOperation));
        Assert.Equal(UpdateMode.InsertOnly, back.UpdateMode);
        Assert.Single(back.Points);
    }

    [Fact]
    public void DeletePointsOperation_DeepRoundTrip_PreservesIds()
    {
        DeletePointsOperation original = Assert.IsType<DeletePointsOperation>(
            UpdateOperation.DeletePoints([PointId.FromNumber(1), PointId.FromNumber(2)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeletePointsOperation);
        DeletePointsOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeletePointsOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeletePointsOperation));
        Assert.Equal(2, back.PointIds.Count);
    }

    [Fact]
    public void DeletePointsByFilterOperation_DeepRoundTrip_PreservesFilter()
    {
        DeletePointsByFilterOperation original = Assert.IsType<DeletePointsByFilterOperation>(
            UpdateOperation.DeletePointsByFilter(CityFilter()));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeletePointsByFilterOperation);
        DeletePointsByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeletePointsByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeletePointsByFilterOperation));
        Assert.Single(back.Filter.Must!);
    }

    [Fact]
    public void UpdateVectorsOperation_DeepRoundTrip_PreservesVectors()
    {
        UpdateVectorsOperation original = Assert.IsType<UpdateVectorsOperation>(
            UpdateOperation.UpdateVectors([new PointVectors { Id = PointId.FromNumber(1), Vector = Vector.Single([1f]) }]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UpdateVectorsOperation);
        UpdateVectorsOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UpdateVectorsOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.UpdateVectorsOperation));
        Assert.Single(back.PointVectors);
    }

    [Fact]
    public void DeleteVectorsByFilterOperation_DeepRoundTrip_PreservesNames()
    {
        DeleteVectorsByFilterOperation original = Assert.IsType<DeleteVectorsByFilterOperation>(
            UpdateOperation.DeleteVectorsByFilter(CityFilter(), [""]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeleteVectorsByFilterOperation);
        DeleteVectorsByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeleteVectorsByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeleteVectorsByFilterOperation));
        Assert.Single(back.VectorNames);
        Assert.Equal("", back.VectorNames[0]);
    }

    [Fact]
    public void DeleteVectorsOperation_DeepRoundTrip_PreservesIdsAndNames()
    {
        DeleteVectorsOperation original = Assert.IsType<DeleteVectorsOperation>(
            UpdateOperation.DeleteVectors([PointId.FromNumber(1)], [""]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeleteVectorsOperation);
        DeleteVectorsOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeleteVectorsOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeleteVectorsOperation));
        Assert.Single(back.PointIds);
        Assert.Single(back.VectorNames);
        Assert.Equal("", back.VectorNames[0]);
    }

    [Fact]
    public void SetPayloadOperation_DeepRoundTrip_PreservesPayloadJson()
    {
        SetPayloadOperation original = Assert.IsType<SetPayloadOperation>(
            UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"city":"Oslo"}"""));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SetPayloadOperation);
        SetPayloadOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SetPayloadOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SetPayloadOperation));
        Assert.Equal("""{"city":"Oslo"}""", back.PayloadJson);
    }

    [Fact]
    public void SetPayloadByFilterOperation_DeepRoundTrip_PreservesKey()
    {
        SetPayloadByFilterOperation original = Assert.IsType<SetPayloadByFilterOperation>(
            UpdateOperation.SetPayloadByFilter(CityFilter(), """{"city":"Oslo"}""", "city"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SetPayloadByFilterOperation);
        SetPayloadByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SetPayloadByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SetPayloadByFilterOperation));
        Assert.Equal("city", back.Key);
    }

    [Fact]
    public void OverwritePayloadOperation_DeepRoundTrip_PreservesPayloadJson()
    {
        OverwritePayloadOperation original = Assert.IsType<OverwritePayloadOperation>(
            UpdateOperation.OverwritePayload([PointId.FromNumber(1)], """{"city":"Oslo"}"""));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.OverwritePayloadOperation);
        OverwritePayloadOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.OverwritePayloadOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.OverwritePayloadOperation));
        Assert.Equal("""{"city":"Oslo"}""", back.PayloadJson);
    }

    [Fact]
    public void OverwritePayloadByFilterOperation_DeepRoundTrip_PreservesPayloadJson()
    {
        OverwritePayloadByFilterOperation original = Assert.IsType<OverwritePayloadByFilterOperation>(
            UpdateOperation.OverwritePayloadByFilter(CityFilter(), """{"city":"Oslo"}"""));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.OverwritePayloadByFilterOperation);
        OverwritePayloadByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.OverwritePayloadByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.OverwritePayloadByFilterOperation));
        Assert.Equal("""{"city":"Oslo"}""", back.PayloadJson);
    }

    [Fact]
    public void DeletePayloadOperation_DeepRoundTrip_PreservesKeys()
    {
        DeletePayloadOperation original = Assert.IsType<DeletePayloadOperation>(
            UpdateOperation.DeletePayload([PointId.FromNumber(1)], ["city", "zip"]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeletePayloadOperation);
        DeletePayloadOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeletePayloadOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeletePayloadOperation));
        Assert.Equal(2, back.Keys.Count);
        Assert.Equal("city", back.Keys[0]);
        Assert.Equal("zip", back.Keys[1]);
    }

    [Fact]
    public void DeletePayloadByFilterOperation_DeepRoundTrip_PreservesKeys()
    {
        DeletePayloadByFilterOperation original = Assert.IsType<DeletePayloadByFilterOperation>(
            UpdateOperation.DeletePayloadByFilter(CityFilter(), ["city"]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeletePayloadByFilterOperation);
        DeletePayloadByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeletePayloadByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.DeletePayloadByFilterOperation));
        Assert.Single(back.Keys);
        Assert.Equal("city", back.Keys[0]);
    }

    [Fact]
    public void ClearPayloadOperation_DeepRoundTrip_PreservesIds()
    {
        ClearPayloadOperation original = Assert.IsType<ClearPayloadOperation>(
            UpdateOperation.ClearPayload([PointId.FromNumber(1)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ClearPayloadOperation);
        ClearPayloadOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ClearPayloadOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ClearPayloadOperation));
        Assert.Single(back.PointIds);
    }

    [Fact]
    public void ClearPayloadByFilterOperation_DeepRoundTrip_PreservesFilter()
    {
        ClearPayloadByFilterOperation original = Assert.IsType<ClearPayloadByFilterOperation>(
            UpdateOperation.ClearPayloadByFilter(CityFilter()));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ClearPayloadByFilterOperation);
        ClearPayloadByFilterOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ClearPayloadByFilterOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ClearPayloadByFilterOperation));
        Assert.Single(back.Filter.Must!);
    }

    [Fact]
    public void CreateFieldIndexOperation_DeepRoundTrip_PreservesSchema()
    {
        CreateFieldIndexOperation original = Assert.IsType<CreateFieldIndexOperation>(
            UpdateOperation.CreateFieldIndex("city", PayloadSchemaType.Keyword));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CreateFieldIndexOperation);
        CreateFieldIndexOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CreateFieldIndexOperation);
        Assert.Equal(original, back);
    }

    [Fact]
    public void CreateFieldIndexWithParamsOperation_DeepRoundTrip_PreservesParams()
    {
        CreateFieldIndexWithParamsOperation original = Assert.IsType<CreateFieldIndexWithParamsOperation>(
            UpdateOperation.CreateFieldIndexWithParams("city", PayloadIndexParams.Keyword(new KeywordIndexParams())));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CreateFieldIndexWithParamsOperation);
        CreateFieldIndexWithParamsOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CreateFieldIndexWithParamsOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.CreateFieldIndexWithParamsOperation));
        Assert.Equal("city", back.FieldName);
        Assert.IsType<KeywordPayloadIndex>(back.IndexParams);
    }

    [Fact]
    public void DeleteFieldIndexOperation_DeepRoundTrip_PreservesFieldName()
    {
        DeleteFieldIndexOperation original = Assert.IsType<DeleteFieldIndexOperation>(
            UpdateOperation.DeleteFieldIndex("city"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeleteFieldIndexOperation);
        DeleteFieldIndexOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeleteFieldIndexOperation);
        Assert.Equal(original, back);
    }

    [Fact]
    public void CreateDenseVectorOperation_DeepRoundTrip_PreservesSize()
    {
        CreateDenseVectorOperation original = Assert.IsType<CreateDenseVectorOperation>(
            UpdateOperation.CreateDenseVector(
                "vec", 4, Distance.Cosine,
                new MultiVectorConfig { Comparator = MultiVectorComparator.MaxSim },
                VectorStorageDatatype.Float32));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CreateDenseVectorOperation);
        CreateDenseVectorOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CreateDenseVectorOperation);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.CreateDenseVectorOperation));
        Assert.Equal(4ul, back.Size);
        Assert.Equal(Distance.Cosine, back.Distance);
    }

    [Fact]
    public void CreateSparseVectorOperation_DeepRoundTrip_PreservesModifier()
    {
        CreateSparseVectorOperation original = Assert.IsType<CreateSparseVectorOperation>(
            UpdateOperation.CreateSparseVector("text", Modifier.Idf, VectorStorageDatatype.Float32));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CreateSparseVectorOperation);
        CreateSparseVectorOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CreateSparseVectorOperation);
        Assert.Equal(original, back);
    }

    [Fact]
    public void DeleteVectorNameOperation_DeepRoundTrip_PreservesName()
    {
        DeleteVectorNameOperation original = Assert.IsType<DeleteVectorNameOperation>(
            UpdateOperation.DeleteVectorName("text"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DeleteVectorNameOperation);
        DeleteVectorNameOperation? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DeleteVectorNameOperation);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> UpdateModeCases()
    {
        yield return [UpdateMode.Upsert];
        yield return [UpdateMode.InsertOnly];
        yield return [UpdateMode.UpdateOnly];
    }

    [Theory]
    [MemberData(nameof(UpdateModeCases))]
    public void UpdateMode_DeepRoundTrip_PreservesValue(UpdateMode original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UpdateMode);
        UpdateMode back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UpdateMode);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> PayloadSchemaTypeCases()
    {
        foreach (PayloadSchemaType value in Enum.GetValues<PayloadSchemaType>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(PayloadSchemaTypeCases))]
    public void PayloadSchemaType_DeepRoundTrip_PreservesValue(PayloadSchemaType original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PayloadSchemaType);
        PayloadSchemaType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadSchemaType);
        Assert.Equal(original, back);
    }

    // Expression entries: base + all 20 concretes.

    [Fact]
    public void Expression_DeepRoundTrip_PreservesConcreteShape()
    {
        // Primary kind pins via the default serializer live in ExpressionGapsTests.cs;
        // this pins the Deep base entry.
        Expression original = Expression.Sum([Expression.Variable("a"), Expression.Constant(2f)]);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Expression);
        Expression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Expression);
        SumExpression typed = Assert.IsType<SumExpression>(back);
        Assert.Equal(2, typed.Terms.Count);
    }

    [Fact]
    public void ConstantExpression_DeepRoundTrip_PreservesValue()
    {
        ConstantExpression original = Assert.IsType<ConstantExpression>(Expression.Constant(1.5f));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ConstantExpression);
        ConstantExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ConstantExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void VariableExpression_DeepRoundTrip_PreservesName()
    {
        VariableExpression original = Assert.IsType<VariableExpression>(Expression.Variable("x"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.VariableExpression);
        VariableExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VariableExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ConditionExpression_DeepRoundTrip_PreservesPredicate()
    {
        ConditionExpression original = Assert.IsType<ConditionExpression>(
            Expression.Condition(Condition.IsEmpty("seen")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ConditionExpression);
        ConditionExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ConditionExpression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.ConditionExpression));
        Assert.Equal(Condition.IsEmpty("seen"), back.Predicate);
    }

    [Fact]
    public void GeoDistanceExpression_DeepRoundTrip_PreservesOrigin()
    {
        GeoDistanceExpression original = Assert.IsType<GeoDistanceExpression>(
            Expression.GeoDistance(new GeoPoint { Lon = 1, Lat = 2 }, "loc"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.GeoDistanceExpression);
        GeoDistanceExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GeoDistanceExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void DatetimeExpression_DeepRoundTrip_PreservesValue()
    {
        DatetimeExpression original = Assert.IsType<DatetimeExpression>(
            Expression.Datetime("2024-01-02T03:04:05Z"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DatetimeExpression);
        DatetimeExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DatetimeExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void DatetimeKeyExpression_DeepRoundTrip_PreservesKey()
    {
        DatetimeKeyExpression original = Assert.IsType<DatetimeKeyExpression>(Expression.DatetimeKey("ts"));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DatetimeKeyExpression);
        DatetimeKeyExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DatetimeKeyExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void MultExpression_DeepRoundTrip_PreservesFactors()
    {
        MultExpression original = Assert.IsType<MultExpression>(
            Expression.Mult([Expression.Variable("a"), Expression.Constant(2f)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MultExpression);
        MultExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MultExpression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.MultExpression));
        Assert.Equal(2, back.Factors.Count);
    }

    [Fact]
    public void SumExpression_DeepRoundTrip_PreservesTerms()
    {
        SumExpression original = Assert.IsType<SumExpression>(
            Expression.Sum([Expression.Variable("a"), Expression.Constant(2f)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SumExpression);
        SumExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SumExpression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.SumExpression));
        Assert.Equal(2, back.Terms.Count);
    }

    [Fact]
    public void MaxExpression_DeepRoundTrip_PreservesOperands()
    {
        MaxExpression original = Assert.IsType<MaxExpression>(
            Expression.Max([Expression.Variable("a"), Expression.Constant(2f)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MaxExpression);
        MaxExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MaxExpression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.MaxExpression));
        Assert.Equal(2, back.Operands.Count);
    }

    [Fact]
    public void MinExpression_DeepRoundTrip_PreservesOperands()
    {
        MinExpression original = Assert.IsType<MinExpression>(
            Expression.Min([Expression.Variable("a"), Expression.Constant(2f)]));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MinExpression);
        MinExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MinExpression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.MinExpression));
        Assert.Equal(2, back.Operands.Count);
    }

    [Fact]
    public void NegateExpression_DeepRoundTrip_PreservesOperand()
    {
        NegateExpression original = Assert.IsType<NegateExpression>(Expression.Negate(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.NegateExpression);
        NegateExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.NegateExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void DivExpression_DeepRoundTrip_PreservesFallback()
    {
        DivExpression original = Assert.IsType<DivExpression>(
            Expression.Div(Expression.Variable("a"), Expression.Constant(2f), 0f));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DivExpression);
        DivExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DivExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void SqrtExpression_DeepRoundTrip_PreservesOperand()
    {
        SqrtExpression original = Assert.IsType<SqrtExpression>(Expression.Sqrt(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SqrtExpression);
        SqrtExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SqrtExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void PowExpression_DeepRoundTrip_PreservesBasisAndExponent()
    {
        PowExpression original = Assert.IsType<PowExpression>(
            Expression.Pow(Expression.Variable("a"), Expression.Constant(2f)));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.PowExpression);
        PowExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PowExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ExpExpression_DeepRoundTrip_PreservesOperand()
    {
        ExpExpression original = Assert.IsType<ExpExpression>(Expression.Exp(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ExpExpression);
        ExpExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ExpExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Log10Expression_DeepRoundTrip_PreservesOperand()
    {
        Log10Expression original = Assert.IsType<Log10Expression>(Expression.Log10(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Log10Expression);
        Log10Expression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Log10Expression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void LnExpression_DeepRoundTrip_PreservesOperand()
    {
        LnExpression original = Assert.IsType<LnExpression>(Expression.Ln(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.LnExpression);
        LnExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.LnExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void AcoshExpression_DeepRoundTrip_PreservesOperand()
    {
        AcoshExpression original = Assert.IsType<AcoshExpression>(Expression.Acosh(Expression.Constant(2f)));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.AcoshExpression);
        AcoshExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.AcoshExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void AbsExpression_DeepRoundTrip_PreservesOperand()
    {
        AbsExpression original = Assert.IsType<AbsExpression>(Expression.Abs(Expression.Variable("a")));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.AbsExpression);
        AbsExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.AbsExpression);
        Assert.Equal(original, back);
    }

    [Fact]
    public void DecayExpression_DeepRoundTrip_PreservesKind()
    {
        DecayExpression original = Assert.IsType<DecayExpression>(
            Expression.Decay(DecayKind.Gauss, Expression.Variable("$score"), Expression.Constant(1f), 0.5f, 2f));
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DecayExpression);
        DecayExpression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DecayExpression);
        Assert.Equal(original, back);
    }

    // Scalar enum entries.

    public static IEnumerable<object[]> DistanceCases()
    {
        foreach (Distance value in Enum.GetValues<Distance>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(DistanceCases))]
    public void Distance_DeepSmoke_RoundTripsAllVariants(Distance original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Distance);
        Distance back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Distance);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> MemoryCases()
    {
        foreach (Memory value in Enum.GetValues<Memory>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(MemoryCases))]
    public void Memory_DeepSmoke_RoundTripsAllVariants(Memory original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Memory);
        Memory back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Memory);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> VectorStorageDatatypeCases()
    {
        foreach (VectorStorageDatatype value in Enum.GetValues<VectorStorageDatatype>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(VectorStorageDatatypeCases))]
    public void VectorStorageDatatype_DeepSmoke_RoundTripsAllVariants(VectorStorageDatatype original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.VectorStorageDatatype);
        VectorStorageDatatype back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VectorStorageDatatype);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> ScalarTypeCases()
    {
        foreach (ScalarType value in Enum.GetValues<ScalarType>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(ScalarTypeCases))]
    public void ScalarType_DeepSmoke_RoundTripsAllVariants(ScalarType original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScalarType);
        ScalarType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScalarType);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> CompressionRatioCases()
    {
        foreach (CompressionRatio value in Enum.GetValues<CompressionRatio>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(CompressionRatioCases))]
    public void CompressionRatio_DeepSmoke_RoundTripsAllVariants(CompressionRatio original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CompressionRatio);
        CompressionRatio back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CompressionRatio);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> MultiVectorConfigCases()
    {
        yield return [new MultiVectorConfig { Comparator = MultiVectorComparator.MaxSim }];
    }

    [Theory]
    [MemberData(nameof(MultiVectorConfigCases))]
    public void MultiVectorConfig_DeepSmoke_RoundTrips(MultiVectorConfig original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MultiVectorConfig);
        MultiVectorConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MultiVectorConfig);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> MultiVectorComparatorCases()
    {
        foreach (MultiVectorComparator value in Enum.GetValues<MultiVectorComparator>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(MultiVectorComparatorCases))]
    public void MultiVectorComparator_DeepRoundTrip_PreservesValue(MultiVectorComparator original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MultiVectorComparator);
        MultiVectorComparator back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MultiVectorComparator);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> BinaryQuantizationEncodingCases()
    {
        foreach (BinaryQuantizationEncoding value in Enum.GetValues<BinaryQuantizationEncoding>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(BinaryQuantizationEncodingCases))]
    public void BinaryQuantizationEncoding_DeepRoundTrip_PreservesValue(BinaryQuantizationEncoding original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.BinaryQuantizationEncoding);
        BinaryQuantizationEncoding back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.BinaryQuantizationEncoding);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> BinaryQuantizationQueryEncodingCases()
    {
        foreach (BinaryQuantizationQueryEncoding value in Enum.GetValues<BinaryQuantizationQueryEncoding>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(BinaryQuantizationQueryEncodingCases))]
    public void BinaryQuantizationQueryEncoding_DeepRoundTrip_PreservesValue(BinaryQuantizationQueryEncoding original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.BinaryQuantizationQueryEncoding);
        BinaryQuantizationQueryEncoding back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.BinaryQuantizationQueryEncoding);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> TurboQuantBitSizeCases()
    {
        foreach (TurboQuantBitSize value in Enum.GetValues<TurboQuantBitSize>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(TurboQuantBitSizeCases))]
    public void TurboQuantBitSize_DeepRoundTrip_PreservesValue(TurboQuantBitSize original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.TurboQuantBitSize);
        TurboQuantBitSize back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.TurboQuantBitSize);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> ModifierCases()
    {
        foreach (Modifier value in Enum.GetValues<Modifier>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(ModifierCases))]
    public void Modifier_DeepRoundTrip_PreservesValue(Modifier original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Modifier);
        Modifier back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Modifier);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> SparseIndexTypeCases()
    {
        foreach (SparseIndexType value in Enum.GetValues<SparseIndexType>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(SparseIndexTypeCases))]
    public void SparseIndexType_DeepRoundTrip_PreservesValue(SparseIndexType original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SparseIndexType);
        SparseIndexType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SparseIndexType);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> DirectionCases()
    {
        foreach (Direction value in Enum.GetValues<Direction>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(DirectionCases))]
    public void Direction_DeepRoundTrip_PreservesValue(Direction original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Direction);
        Direction back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Direction);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> SampleCases()
    {
        foreach (Sample value in Enum.GetValues<Sample>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(SampleCases))]
    public void Sample_DeepRoundTrip_PreservesValue(Sample original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Sample);
        Sample back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Sample);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> RecommendStrategyCases()
    {
        foreach (RecommendStrategy value in Enum.GetValues<RecommendStrategy>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(RecommendStrategyCases))]
    public void RecommendStrategy_DeepRoundTrip_PreservesValue(RecommendStrategy original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RecommendStrategy);
        RecommendStrategy back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RecommendStrategy);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> DecayKindCases()
    {
        foreach (DecayKind value in Enum.GetValues<DecayKind>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(DecayKindCases))]
    public void DecayKind_DeepRoundTrip_PreservesValue(DecayKind original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.DecayKind);
        DecayKind back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.DecayKind);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> TokenizerTypeCases()
    {
        foreach (TokenizerType value in Enum.GetValues<TokenizerType>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(TokenizerTypeCases))]
    public void TokenizerType_DeepSmoke_RoundTripsAllVariants(TokenizerType original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.TokenizerType);
        TokenizerType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.TokenizerType);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> LanguageCases()
    {
        foreach (Language value in Enum.GetValues<Language>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(LanguageCases))]
    public void Language_DeepSmoke_RoundTripsAllVariants(Language original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Language);
        Language back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Language);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> SnowballLanguageCases()
    {
        foreach (SnowballLanguage value in Enum.GetValues<SnowballLanguage>())
        {
            yield return [value];
        }
    }

    [Theory]
    [MemberData(nameof(SnowballLanguageCases))]
    public void SnowballLanguage_DeepSmoke_RoundTripsAllVariants(SnowballLanguage original)
    {
        // Primary pins live in SerializationRoundTripTests.cs; this is the §18 checklist smoke.
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SnowballLanguage);
        SnowballLanguage back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SnowballLanguage);
        Assert.Equal(original, back);
    }

    // Internal request entries (no Deep round-trip anywhere else).

    [Fact]
    public void VectorHnswRequest_DeepRoundTrip_PreservesVectorName()
    {
        VectorHnswRequest original = new("", new HnswIndexConfig { M = 16, EfConstruct = 100 });
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.VectorHnswRequest);
        VectorHnswRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VectorHnswRequest);
        Assert.Equal(original, back);
    }

    [Fact]
    public void SnapshotRequest_DeepRoundTrip_PreservesPath()
    {
        SnapshotRequest original = new("/tmp/snapshots/shard", null);
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SnapshotRequest);
        SnapshotRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SnapshotRequest);
        Assert.Equal(original, back);
    }

    [Fact]
    public void UnpackRequest_DeepRoundTrip_PreservesPaths()
    {
        UnpackRequest original = new("/tmp/archive", "/tmp/target");
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.UnpackRequest);
        UnpackRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.UnpackRequest);
        Assert.Equal(original, back);
    }

    // Depth and singleton pins.

    [Fact]
    public void Deep_MaxDepth_Is256WhileDefaultIsUnset()
    {
        Assert.Equal(256, EdgeJsonContext.Deep.Options.MaxDepth);
        // 0 leaves the STJ default (64) in force; Deep raises it explicitly.
        Assert.Equal(0, EdgeJsonContext.Default.Options.MaxDepth);
    }

    [Fact]
    public void Deep_IsSingleton_ReturnsSameInstance()
    {
        Assert.Same(EdgeJsonContext.Deep, EdgeJsonContext.Deep);
    }

    [Fact]
    public void Deep_Serializes64DeepFilter_Succeeds()
    {
        Filter inner = new() { Must = [Condition.IsEmpty("leaf")] };
        for (int i = 0; i < 64; i++)
        {
            inner = new Filter { Must = [Condition.Nested("n", inner)] };
        }

        Assert.Equal(64u, inner.Depth());
        inner.Validate();
        string json = JsonSerializer.Serialize(inner, EdgeJsonContext.Deep.Filter);
        Assert.NotEmpty(json);
        Filter? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Filter);
        Assert.NotNull(back);
        Assert.Equal(64u, back.Depth());
    }

    [Fact]
    public void Deep_SerializesDeepFormula_Succeeds()
    {
        Expression expression = Expression.Variable("x");
        for (int i = 0; i < 63; i++)
        {
            expression = Expression.Negate(expression);
        }

        Assert.Equal(64u, expression.Depth);
        expression.Validate();
        string json = JsonSerializer.Serialize(expression, EdgeJsonContext.Deep.Expression);
        Assert.NotEmpty(json);
        Expression? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Expression);
        Assert.NotNull(back);
        Assert.Equal(json, JsonSerializer.Serialize(back, EdgeJsonContext.Deep.Expression));
    }
}
