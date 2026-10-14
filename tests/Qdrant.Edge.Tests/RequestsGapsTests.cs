using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Validation and JSON pins for every <c>Requests.cs</c> arm (§12).</summary>
public sealed class RequestsGapsTests
{
    private static NamedVector Dense() => NamedVector.Dense([1f, 2f, 3f, 4f]);

    private static NamedVector InvalidDense() => NamedVector.Dense([]);

    private static Query InvalidVectorQuery() => Query.Nearest(InvalidDense());

    private static ScoringQuery ValidScoring() => ScoringQuery.CreateVector(Query.Nearest(Dense()));

    private static ScoringQuery InvalidScoring() => ScoringQuery.CreateVector(InvalidVectorQuery());

    private static Filter InvalidFilter() => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = "k" })],
    };

    private static QueryRequest ValidQueryRequest(ulong limit = 10) => new()
    {
        Limit = limit,
        Query = ValidScoring(),
    };

    // Prefetch

    [Fact]
    public void Prefetch_NullNested_ThrowsArgumentNull()
    {
        Prefetch prefetch = new() { Limit = 10, Prefetches = [null!] };
        Assert.Throws<ArgumentNullException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_InvalidNested_Propagates()
    {
        Prefetch prefetch = new()
        {
            Limit = 10,
            Prefetches = [new Prefetch { Limit = EdgeValidation.MaxResultCount + 1 }],
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_InvalidFilter_Propagates()
    {
        Prefetch prefetch = new() { Limit = 10, Filter = InvalidFilter() };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_InvalidQuery_Propagates()
    {
        Prefetch prefetch = new() { Limit = 10, Query = InvalidScoring() };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_NaNThreshold_Throws()
    {
        Prefetch prefetch = new() { Limit = 10, ScoreThreshold = float.NaN };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_PosInfThreshold_Throws()
    {
        Prefetch prefetch = new() { Limit = 10, ScoreThreshold = float.PositiveInfinity };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_NegInfThreshold_Throws()
    {
        Prefetch prefetch = new() { Limit = 10, ScoreThreshold = float.NegativeInfinity };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    [Fact]
    public void Prefetch_NullThreshold_Passes()
    {
        Prefetch prefetch = new() { Limit = 10, ScoreThreshold = null };
        prefetch.Validate();
    }

    [Fact]
    public void Prefetch_LimitAtCap_Passes()
    {
        Prefetch prefetch = new() { Limit = EdgeValidation.MaxResultCount };
        prefetch.Validate();
    }

    [Fact]
    public void Prefetch_LimitAboveCap_Throws()
    {
        Prefetch prefetch = new() { Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => prefetch.Validate());
    }

    // SearchRequest

    [Fact]
    public void Search_NullQuery_Throws()
    {
        SearchRequest request = new() { Query = null!, Limit = 10 };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Search_InvalidQuery_Propagates()
    {
        SearchRequest request = new() { Query = InvalidVectorQuery(), Limit = 10 };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Search_LimitAtCap_Passes()
    {
        SearchRequest request = new() { Query = Query.Nearest(Dense()), Limit = EdgeValidation.MaxResultCount };
        request.Validate();
    }

    [Fact]
    public void Search_LimitZero_Passes()
    {
        SearchRequest request = new() { Query = Query.Nearest(Dense()), Limit = 0 };
        request.Validate();
    }

    [Fact]
    public void Search_OffsetNull_Passes()
    {
        SearchRequest request = new() { Query = Query.Nearest(Dense()), Limit = 10, Offset = null };
        request.Validate();
    }

    [Fact]
    public void Search_OffsetAtCap_Passes()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            Offset = EdgeValidation.MaxResultCount,
        };
        request.Validate();
    }

    [Fact]
    public void Search_OffsetAboveCap_Throws()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            Offset = EdgeValidation.MaxResultCount + 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Search_InvalidFilter_Propagates()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            Filter = InvalidFilter(),
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Search_NaNThreshold_Throws()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            ScoreThreshold = float.NaN,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Search_PosInfThreshold_Throws()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            ScoreThreshold = float.PositiveInfinity,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Search_NegInfThreshold_Throws()
    {
        SearchRequest request = new()
        {
            Query = Query.Nearest(Dense()),
            Limit = 10,
            ScoreThreshold = float.NegativeInfinity,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    // QueryRequest

    [Fact]
    public void QueryRequest_NullPrefetchElement_Throws()
    {
        QueryRequest request = new() { Limit = 10, Prefetches = [null!] };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_InvalidPrefetch_Propagates()
    {
        QueryRequest request = new()
        {
            Limit = 10,
            Prefetches = [new Prefetch { Limit = EdgeValidation.MaxResultCount + 1 }],
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_InvalidQuery_Propagates()
    {
        QueryRequest request = new() { Limit = 10, Query = InvalidScoring() };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_InvalidFilter_Propagates()
    {
        QueryRequest request = new() { Limit = 10, Filter = InvalidFilter() };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_NullQuery_PassesValidate()
    {
        QueryRequest request = new() { Limit = 10, Query = null };
        request.Validate();
    }

    [Fact]
    public void QueryRequest_LimitAtCap_Passes()
    {
        ValidQueryRequest(EdgeValidation.MaxResultCount).Validate();
    }

    [Fact]
    public void QueryRequest_LimitZero_Passes()
    {
        ValidQueryRequest(0).Validate();
    }

    [Fact]
    public void QueryRequest_OffsetNull_Passes()
    {
        QueryRequest request = ValidQueryRequest() with { Offset = null };
        request.Validate();
    }

    [Fact]
    public void QueryRequest_OffsetAtCap_Passes()
    {
        QueryRequest request = ValidQueryRequest() with { Offset = EdgeValidation.MaxResultCount };
        request.Validate();
    }

    [Fact]
    public void QueryRequest_OffsetAboveCap_Throws()
    {
        QueryRequest request = ValidQueryRequest() with { Offset = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_NaNThreshold_Throws()
    {
        QueryRequest request = ValidQueryRequest() with { ScoreThreshold = float.NaN };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_PosInfThreshold_Throws()
    {
        QueryRequest request = ValidQueryRequest() with { ScoreThreshold = float.PositiveInfinity };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryRequest_NegInfThreshold_Throws()
    {
        QueryRequest request = ValidQueryRequest() with { ScoreThreshold = float.NegativeInfinity };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    // QueryBatchRequest

    [Fact]
    public void QueryBatch_NullQueries_Throws()
    {
        QueryBatchRequest request = new() { Queries = null! };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void QueryBatch_NullElement_Throws()
    {
        QueryBatchRequest request = new() { Queries = [null!] };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void QueryBatch_InvalidElement_Propagates()
    {
        QueryBatchRequest request = new()
        {
            Queries = [new QueryRequest { Limit = EdgeValidation.MaxResultCount + 1 }],
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void QueryBatch_SingleQuery_Passes()
    {
        QueryBatchRequest request = new() { Queries = [ValidQueryRequest()] };
        request.Validate();
    }

    [Fact]
    public void QueryBatch_InvalidSecondQuery_Throws()
    {
        QueryBatchRequest request = new()
        {
            Queries =
            [
                ValidQueryRequest(),
                new QueryRequest { Limit = EdgeValidation.MaxResultCount + 1 },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    // ScrollRequest

    [Fact]
    public void Scroll_NullLimit_Passes()
    {
        ScrollRequest request = new() { Limit = null };
        request.Validate();
    }

    [Fact]
    public void Scroll_LimitZero_Passes()
    {
        ScrollRequest request = new() { Limit = 0 };
        request.Validate();
    }

    [Fact]
    public void Scroll_LimitAtCap_Passes()
    {
        ScrollRequest request = new() { Limit = EdgeValidation.MaxResultCount };
        request.Validate();
    }

    [Fact]
    public void Scroll_InvalidFilter_Propagates()
    {
        ScrollRequest request = new() { Limit = 10, Filter = InvalidFilter() };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Scroll_OrderByNotValidated_Passes()
    {
        ScrollRequest request = new() { Limit = 10, OrderBy = new OrderBy { Key = string.Empty } };
        request.Validate();
    }

    [Fact]
    public void Scroll_EmptyOrderBy_Native_RejectsWithoutAbortingShard()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard(out _);
        EdgeInvalidArgumentException thrown = Assert.Throws<EdgeInvalidArgumentException>(() => shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = string.Empty },
        }));
        Assert.NotEmpty(thrown.Reason);
        Assert.Equal(2ul, shard.Count(new CountRequest()));
    }

    // RetrieveRequest

    [Fact]
    public void Retrieve_NullIds_Throws()
    {
        RetrieveRequest request = new() { PointIds = null! };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Retrieve_NullElement_Throws()
    {
        RetrieveRequest request = new() { PointIds = [null!] };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Retrieve_EmptyIds_PassesValidate()
    {
        RetrieveRequest request = new() { PointIds = [] };
        request.Validate();
    }

    [Fact]
    public void Retrieve_DuplicateIds_PassesValidate()
    {
        RetrieveRequest request = new() { PointIds = [PointId.FromNumber(1), PointId.FromNumber(1)] };
        request.Validate();
    }

    // CountRequest

    [Fact]
    public void Count_InvalidFilter_Propagates()
    {
        CountRequest request = new() { Filter = InvalidFilter() };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Count_ExactFalse_PassesValidate()
    {
        CountRequest request = new() { Exact = false };
        request.Validate();
    }

    [Fact]
    public void Count_ExactFalse_Native_ReturnsSeededCount()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest { Exact = false }));
        Assert.Equal(2ul, shard.Count(new CountRequest { Exact = true }));
    }

    // FacetRequest

    [Fact]
    public void Facet_NullKey_ThrowsArgumentNull()
    {
        FacetRequest request = new() { Key = null! };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Facet_EmptyKey_ThrowsArgument()
    {
        FacetRequest request = new() { Key = string.Empty };
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Facet_WhitespaceKey_ThrowsArgument()
    {
        FacetRequest request = new() { Key = "  " };
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Facet_InvalidFilter_Propagates()
    {
        FacetRequest request = new() { Key = "city", Filter = InvalidFilter() };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Facet_LimitZero_Passes()
    {
        FacetRequest request = new() { Key = "city", Limit = 0 };
        request.Validate();
    }

    [Fact]
    public void Facet_LimitAtCap_Passes()
    {
        FacetRequest request = new() { Key = "city", Limit = EdgeValidation.MaxResultCount };
        request.Validate();
    }

    [Fact]
    public void Facet_LimitAboveCap_Throws()
    {
        FacetRequest request = new() { Key = "city", Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Facet_ExactTrue_Native_ReturnsSeededHits()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard(out _);
        FacetResponse response = shard.Facet(new FacetRequest { Key = "city", Exact = true });
        Assert.Equal(
            ["blue", "red"],
            response.Hits.OrderBy(hit => hit.Value).Select(hit => hit.Value));
        Assert.All(response.Hits, hit => Assert.Equal(1ul, hit.Count));
    }

    [Fact]
    public void Facet_MissingKey_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityShard(out _);
        EdgeOperationException thrown = Assert.Throws<EdgeOperationException>(
            () => shard.Facet(new FacetRequest { Key = "missing" }));
        Assert.Contains("missing", thrown.Message);
        Assert.Equal(2ul, shard.Count(new CountRequest()));
    }

    // FacetResponse

    [Fact]
    public void FacetResponse_EmptyHits_Passes()
    {
        FacetResponse? response = JsonSerializer.Deserialize(
            """{"Hits":[]}""", EdgeJsonContext.Deep.FacetResponse);
        Assert.NotNull(response);
        Assert.Equal(0, response.Count);
        Assert.Empty(response.Hits);
    }

    [Fact]
    public void FacetResponse_IndexerOutOfRange_Throws()
    {
        FacetResponse response = new() { Hits = [new FacetHit { Value = "red", Count = 3 }] };
        Assert.Throws<IndexOutOfRangeException>(() => response[5]);
    }

    // GroupRequest

    [Fact]
    public void Group_NullQuery_Throws()
    {
        GroupRequest request = new()
        {
            Query = null!,
            GroupBy = "city",
            Groups = 5,
            GroupSize = 1,
        };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Group_InvalidQuery_Propagates()
    {
        GroupRequest request = new()
        {
            Query = new QueryRequest { Limit = EdgeValidation.MaxResultCount + 1 },
            GroupBy = "city",
            Groups = 5,
            GroupSize = 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Group_NullGroupBy_ThrowsArgumentNull()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = null!,
            Groups = 5,
            GroupSize = 1,
        };
        Assert.Throws<ArgumentNullException>(request.Validate);
    }

    [Fact]
    public void Group_EmptyGroupBy_ThrowsArgument()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = string.Empty,
            Groups = 5,
            GroupSize = 1,
        };
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Group_WhitespaceGroupBy_ThrowsArgument()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "  ",
            Groups = 5,
            GroupSize = 1,
        };
        Assert.Throws<ArgumentException>(request.Validate);
    }

    [Fact]
    public void Group_GroupsZero_PassesValidate()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "category",
            Groups = 0,
            GroupSize = 1,
        };
        request.Validate();
    }

    [Fact]
    public void Group_GroupSizeZero_PassesValidate()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "category",
            Groups = 5,
            GroupSize = 0,
        };
        request.Validate();
    }

    [Fact]
    public void Group_GroupsAtCap_Passes()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "category",
            Groups = EdgeValidation.MaxResultCount,
            GroupSize = 1,
        };
        request.Validate();
    }

    [Fact]
    public void Group_GroupSizeAtCap_Passes()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "category",
            Groups = 5,
            GroupSize = EdgeValidation.MaxResultCount,
        };
        request.Validate();
    }

    [Fact]
    public void Group_GroupsAboveCap_Throws()
    {
        GroupRequest request = new()
        {
            Query = ValidQueryRequest(),
            GroupBy = "category",
            Groups = EdgeValidation.MaxResultCount + 1,
            GroupSize = 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void Group_ZeroGroups_Native_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = new QueryRequest
            {
                Limit = 10,
                Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            },
            GroupBy = "category",
            Groups = 0,
            GroupSize = 1,
        });
        Assert.Empty(groups);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Group_EmptyHits_Passes()
    {
        Group group = new() { Key = new StringGroupId("a"), Hits = [] };
        Assert.Empty(group.Hits);
    }

    public static TheoryData<GroupId> GroupIdCases() => new()
    {
        new StringGroupId("a"),
        new UInt64GroupId(7),
        new Int64GroupId(-3),
    };

    [Theory]
    [MemberData(nameof(GroupIdCases))]
    public void GroupId_All3Kinds_RoundTripThroughJson(GroupId key)
    {
        string json = JsonSerializer.Serialize(key, EdgeJsonContext.Deep.GroupId);
        GroupId? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.GroupId);
        Assert.Equal(key, back);
    }

    // SearchMatrixRequest

    [Fact]
    public void SearchMatrix_SampleZero_PassesValidate()
    {
        SearchMatrixRequest request = new() { SampleSize = 0, LimitPerSample = 1 };
        request.Validate();
    }

    [Fact]
    public void SearchMatrix_LimitZero_PassesValidate()
    {
        SearchMatrixRequest request = new() { SampleSize = 2, LimitPerSample = 0 };
        request.Validate();
    }

    [Fact]
    public void SearchMatrix_SampleAtCap_Passes()
    {
        SearchMatrixRequest request = new()
        {
            SampleSize = EdgeValidation.MaxResultCount,
            LimitPerSample = 1,
        };
        request.Validate();
    }

    [Fact]
    public void SearchMatrix_SampleAboveCap_Throws()
    {
        SearchMatrixRequest request = new()
        {
            SampleSize = EdgeValidation.MaxResultCount + 1,
            LimitPerSample = 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void SearchMatrix_LimitAtCap_Passes()
    {
        SearchMatrixRequest request = new()
        {
            SampleSize = 2,
            LimitPerSample = EdgeValidation.MaxResultCount,
        };
        request.Validate();
    }

    [Fact]
    public void SearchMatrix_LimitAboveCap_Throws()
    {
        SearchMatrixRequest request = new()
        {
            SampleSize = 2,
            LimitPerSample = EdgeValidation.MaxResultCount + 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void SearchMatrix_InvalidFilter_Propagates()
    {
        SearchMatrixRequest request = new()
        {
            SampleSize = 2,
            LimitPerSample = 1,
            Filter = InvalidFilter(),
        };
        Assert.Throws<EdgeInvalidArgumentException>(request.Validate);
    }

    [Fact]
    public void SearchMatrix_UsingPreserved()
    {
        SearchMatrixRequest request = new() { SampleSize = 2, LimitPerSample = 1, Using = "image" };
        request.Validate();
        string json = JsonSerializer.Serialize(request, EdgeJsonContext.Deep.SearchMatrixRequest);
        SearchMatrixRequest? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchMatrixRequest);
        Assert.NotNull(back);
        Assert.Equal("image", back.Using);
        SearchMatrixRequest anonymous = new() { SampleSize = 2, LimitPerSample = 1 };
        anonymous.Validate();
        Assert.Null(anonymous.Using);
    }

#if QDRANT_EDGE_MATRIX
#pragma warning disable QDRANT_EDGE_MATRIX
    [Fact]
    public void SearchMatrix_RankedShard_ReturnsSamplesAndNearests()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.RankedShard(out _);
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 2,
            LimitPerSample = 1,
        });
        Assert.Equal(2, response.SampleIds.Count);
        Assert.Equal(2, response.Nearests.Count);
        Assert.All(response.Nearests, near => Assert.True(near.Count <= 1));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }
#pragma warning restore QDRANT_EDGE_MATRIX
#endif

    // SearchMatrixResponse / ShardInfo

    [Fact]
    public void SearchMatrixResponse_Empty_RoundTrips()
    {
        SearchMatrixResponse response = new() { SampleIds = [], Nearests = [] };
        string json = JsonSerializer.Serialize(response, EdgeJsonContext.Deep.SearchMatrixResponse);
        SearchMatrixResponse? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchMatrixResponse);
        Assert.NotNull(back);
        Assert.Empty(back.SampleIds);
        Assert.Empty(back.Nearests);
    }

    [Fact]
    public void ShardInfo_EmptySchema_RoundTrips()
    {
        ShardInfo info = new()
        {
            SegmentsCount = 1,
            PointsCount = 2,
            IndexedVectorsCount = 2,
            PayloadSchema = new Dictionary<string, PayloadIndexInfo>(),
        };
        string json = JsonSerializer.Serialize(info, EdgeJsonContext.Deep.ShardInfo);
        ShardInfo? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ShardInfo);
        Assert.NotNull(back);
        Assert.Equal(1ul, back.SegmentsCount);
        Assert.Equal(2ul, back.PointsCount);
        Assert.Empty(back.PayloadSchema);
    }

    // PayloadIndexInfo

    public static IEnumerable<object?[]> PayloadIndexCases()
    {
        foreach (PayloadSchemaType dataType in Enum.GetValues<PayloadSchemaType>())
        {
            yield return [dataType, null];
        }

        yield return [PayloadSchemaType.Keyword, PayloadIndexParams.Keyword(new KeywordIndexParams())];
        yield return [PayloadSchemaType.Integer, PayloadIndexParams.Integer(new IntegerIndexParams())];
        yield return [PayloadSchemaType.Float, PayloadIndexParams.Float(new FloatIndexParams())];
        yield return [PayloadSchemaType.Geo, PayloadIndexParams.Geo(new GeoIndexParams())];
        yield return [PayloadSchemaType.Text, PayloadIndexParams.Text(new TextIndexParams())];
        yield return [PayloadSchemaType.Bool, PayloadIndexParams.Bool(new BoolIndexParams())];
        yield return [PayloadSchemaType.Datetime, PayloadIndexParams.Datetime(new DatetimeIndexParams())];
        yield return [PayloadSchemaType.Uuid, PayloadIndexParams.Uuid(new UuidIndexParams())];
    }

    [Theory]
    [MemberData(nameof(PayloadIndexCases))]
    public void PayloadIndexInfo_EachDataType_RoundTrips(PayloadSchemaType dataType, PayloadIndexParams? indexParams)
    {
        PayloadIndexInfo info = new() { DataType = dataType, Params = indexParams, Points = 7 };
        string json = JsonSerializer.Serialize(info, EdgeJsonContext.Deep.PayloadIndexInfo);
        PayloadIndexInfo? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexInfo);
        Assert.NotNull(back);
        Assert.Equal(dataType, back.DataType);
        Assert.Equal(7ul, back.Points);
        if (indexParams is null)
        {
            Assert.Null(back.Params);
        }
        else
        {
            Assert.NotNull(back.Params);
            Assert.Equal(indexParams.GetType(), back.Params.GetType());
        }
    }

    private static EdgeShard CityShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("city", PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 6f, 9f, 4f, 2f) with
                {
                    Payload = new JsonObject { ["city"] = "red" },
                },
                TestHelpers.FourDimPoint(2, 1f, 2f, 3f, 4f) with
                {
                    Payload = new JsonObject { ["city"] = "blue" },
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
