using System.Formats.Tar;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Section 15 pins for <c>EdgeShard</c>: lifecycle, guards, error mapping, async, serialization.</summary>
public sealed class EdgeShardGapsTests
{
    private static readonly CancellationToken Cancelled = new(canceled: true);

    /// <summary>
    /// Instance whose guards run without native dispatch: invalid handle, so every
    /// null/cancelled/invalid check throws before any P/Invoke. Never unload this instance.
    /// </summary>
    private static EdgeShard GuardOnlyShard()
    {
        ConstructorInfo? ctor = typeof(EdgeShard).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(EdgeShardHandle), typeof(string)]);
        Assert.NotNull(ctor);
        return (EdgeShard)ctor!.Invoke(
            [new EdgeShardHandle(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))]);
    }

    private static SearchRequest ValidSearch() => new()
    {
        Query = Query.Nearest(NamedVector.Dense([1f, 2f, 3f, 4f])),
        Limit = 10,
    };

    private static QueryRequest ValidQuery() => new()
    {
        Limit = 10,
        Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 2f, 3f, 4f]))),
    };

    private static QueryBatchRequest ValidBatch() => new()
    {
        Queries = [new QueryRequest { Limit = 5 }],
    };

    private static GroupRequest ValidGroups() => new()
    {
        Query = new QueryRequest { Limit = 10 },
        GroupBy = "city",
        Groups = 10,
        GroupSize = 5,
    };

    private static ScrollRequest ValidScroll() => new() { Limit = 10 };

    private static RetrieveRequest ValidRetrieve() => new()
    {
        PointIds = [PointId.FromNumber(1)],
    };

    private static UpdateOperation ValidUpdate() =>
        UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]);

    private static void InvokeOp(EdgeShard shard, string op)
    {
        switch (op)
        {
            case "Search": shard.Search(ValidSearch()); break;
            case "Query": shard.Query(ValidQuery()); break;
            case "QueryBatch": shard.QueryBatch(ValidBatch()); break;
            case "QueryGroups": shard.QueryGroups(ValidGroups()); break;
            case "Scroll": shard.Scroll(ValidScroll()); break;
            case "Retrieve": shard.Retrieve(ValidRetrieve()); break;
            case "Count": shard.Count(new CountRequest()); break;
            case "Facet": shard.Facet(new FacetRequest { Key = "city" }); break;
            case "GetInfo": shard.GetInfo(); break;
            case "Update": shard.Update(ValidUpdate()); break;
            case "Flush": shard.Flush(); break;
            case "Optimize": shard.Optimize(); break;
            case "GetConfig": shard.GetConfig(); break;
            case "SetHnsw": shard.SetHnswConfig(new HnswIndexConfig()); break;
            case "SetVectorHnsw": shard.SetVectorHnswConfig("", new HnswIndexConfig()); break;
            case "SetOptimizers": shard.SetOptimizersConfig(new OptimizersConfig { MaxSegmentSizeKb = 1024 }); break;
            case "SnapshotManifest": shard.SnapshotManifest(); break;
            case "UpdateFromSnapshot":
                shard.UpdateFromSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
                break;
            case "Path": _ = shard.Path; break;
            default: throw new InvalidOperationException($"unknown op {op}");
        }
    }

    // Create guards: ThrowIfNullOrWhiteSpace, cancellation, config.Validate all run before native.

    [Fact]
    public void Create_NullPath_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeShard.Create(null!, TestHelpers.FourDimDotConfig()));
    }

    [Fact]
    public void Create_EmptyStringPath_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => EdgeShard.Create(string.Empty, TestHelpers.FourDimDotConfig()));
    }

    [Fact]
    public void Create_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(
            () => EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig(), Cancelled));
    }

    [Fact]
    public void Create_InvalidConfig_ThrowsBeforeNative()
    {
        string subpath = Path.Combine(TestHelpers.TempDir(), "never-created");
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeShard.Create(subpath, new EdgeConfig()));
        Assert.False(Directory.Exists(subpath));
    }

    // Load guards: same ordering — path, cancellation, config validation precede LoadCore.

    [Fact]
    public void Load_NullPath_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeShard.Load(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Load_EmptyAndWhitespacePath_ThrowsArgument(string path)
    {
        Assert.Throws<ArgumentException>(() => EdgeShard.Load(path));
    }

    [Fact]
    public void Load_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => EdgeShard.Load(TestHelpers.TempDir(), null, Cancelled));
    }

    [Fact]
    public void Load_InvalidConfig_ThrowsBeforeNative()
    {
        string subpath = Path.Combine(TestHelpers.TempDir(), "never-created");
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeShard.Load(subpath, new EdgeConfig()));
        Assert.False(Directory.Exists(subpath));
    }

    [Fact]
    public void Load_NullConfig_ReachesNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(TestHelpers.TempDir(), null));
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Unload();
        shard.Dispose();
        using EdgeShard reloaded = EdgeShard.Load(dir, null);
        Assert.Equal(0ul, reloaded.Count(new CountRequest()));
    }

    // Path and IsLoaded transitions.

    [Fact]
    public void Path_AfterUnload_ThrowsShardClosed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        try
        {
            shard.Unload();
            Assert.Throws<ShardClosedException>(() => shard.Path);
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Fact]
    public void Path_AfterDispose_ThrowsShardClosed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Dispose();
        Assert.Throws<ShardClosedException>(() => shard.Path);
    }

    [Fact]
    public void IsLoaded_TrueAfterCreate()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void IsLoaded_FalseAfterUnload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        try
        {
            shard.Unload();
            Assert.False(shard.IsLoaded);
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Fact]
    public void IsLoaded_FalseAfterDispose()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Dispose();
        Assert.False(shard.IsLoaded);
    }

    [Fact]
    public void Load_MissingDirectory_ThrowsInsteadOfInstance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = Path.Combine(TestHelpers.TempDir(), "nope");
        Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(dir));
    }

    // Unload/Dispose idempotence matrix.

    [Fact]
    public void Unload_AfterFailedOperation_StaysIdempotent()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        Assert.Throws<EdgeOperationException>(() =>
            shard.UpdateFromSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        shard.Unload();
        shard.Unload();
        Assert.False(shard.IsLoaded);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Dispose();
        shard.Dispose();
    }

    [Fact]
    public void Dispose_AfterUnload_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Unload();
        shard.Dispose();
    }

    [Fact]
    public void Unload_AfterDispose_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Dispose();
        shard.Unload();
    }

    // Closed-guard matrices: every op after Unload or Dispose throws ShardClosedException.

    public static TheoryData<string> ClosedOps() => new()
    {
        "Search", "Query", "QueryBatch", "QueryGroups", "Scroll", "Retrieve", "Count",
        "Facet", "GetInfo", "Update", "Flush", "Optimize", "GetConfig", "SetHnsw",
        "SetVectorHnsw", "SetOptimizers", "SnapshotManifest", "UpdateFromSnapshot", "Path",
    };

    [Theory]
    [MemberData(nameof(ClosedOps))]
    public void OpsAfterUnload_AllThrowShardClosed(string op)
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        try
        {
            shard.Unload();
            Assert.Throws<ShardClosedException>(() => InvokeOp(shard, op));
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(ClosedOps))]
    public void OpsAfterDispose_AllThrowShardClosed(string op)
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Dispose();
        Assert.Throws<ShardClosedException>(() => InvokeOp(shard, op));
    }

    // Config setters: null, invalid, and empty-name arms (guards run before native).

    [Fact]
    public void SetHnswConfig_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().SetHnswConfig(null!));
    }

    [Fact]
    public void SetHnswConfig_Invalid_ThrowsBeforeNative()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => GuardOnlyShard().SetHnswConfig(new HnswIndexConfig { M = EdgeValidation.MaxHnswM + 1 }));
    }

    [Fact]
    public void SetVectorHnswConfig_NullName_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => GuardOnlyShard().SetVectorHnswConfig(null!, new HnswIndexConfig()));
    }

    [Fact]
    public void SetVectorHnswConfig_NullHnsw_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => GuardOnlyShard().SetVectorHnswConfig("image", null!));
    }

    [Fact]
    public void SetVectorHnswConfig_InvalidHnsw_ThrowsBeforeNative()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => GuardOnlyShard().SetVectorHnswConfig(
                "image", new HnswIndexConfig { M = EdgeValidation.MaxHnswM + 1 }));
    }

    [Fact]
    public void SetVectorHnswConfig_EmptyName_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.SetVectorHnswConfig(string.Empty, new HnswIndexConfig());
        Assert.Equal(0ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SetOptimizersConfig_Null_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().SetOptimizersConfig(null!));
    }

    [Fact]
    public void SetOptimizersConfig_Invalid_ThrowsBeforeNative()
    {
        Assert.Throws<EdgeInvalidArgumentException>(
            () => GuardOnlyShard().SetOptimizersConfig(new OptimizersConfig { DeletedThreshold = 1.5 }));
    }

    // Update triplet.

    [Fact]
    public void Update_NullOperation_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Update(null!));
    }

    [Fact]
    public void Update_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().Update(ValidUpdate(), Cancelled));
    }

    [Fact]
    public void Update_InvalidOperation_ThrowsBeforeNative()
    {
        UpdateOperation forged = new UpsertPointsOperation(
            [new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([]) }], null, null);
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Update(forged));
    }

    // Search triplet.

    [Fact]
    public void Search_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Search(null!));
    }

    [Fact]
    public void Search_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().Search(ValidSearch(), Cancelled));
    }

    [Fact]
    public void Search_InvalidRequest_ThrowsBeforeNative()
    {
        SearchRequest forged = ValidSearch() with { Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Search(forged));
    }

    // Query triplet.

    [Fact]
    public void Query_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Query(null!));
    }

    [Fact]
    public void Query_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().Query(ValidQuery(), Cancelled));
    }

    [Fact]
    public void Query_InvalidRequest_ThrowsBeforeNative()
    {
        QueryRequest forged = ValidQuery() with { Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Query(forged));
    }

    // QueryBatch triplet.

    [Fact]
    public void QueryBatch_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().QueryBatch(null!));
    }

    [Fact]
    public void QueryBatch_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().QueryBatch(ValidBatch(), Cancelled));
    }

    [Fact]
    public void QueryBatch_InvalidSecondQuery_ThrowsBeforeNative()
    {
        var request = new QueryBatchRequest
        {
            Queries =
            [
                new QueryRequest { Limit = 5 },
                new QueryRequest { Limit = EdgeValidation.MaxResultCount + 1 },
            ],
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().QueryBatch(request));
    }

    // QueryGroups triplet.

    [Fact]
    public void QueryGroups_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().QueryGroups(null!));
    }

    [Fact]
    public void QueryGroups_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().QueryGroups(ValidGroups(), Cancelled));
    }

    [Fact]
    public void QueryGroups_InvalidRequest_ThrowsBeforeNative()
    {
        GroupRequest forged = ValidGroups() with { Groups = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().QueryGroups(forged));
    }

    // Scroll triplet.

    [Fact]
    public void Scroll_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Scroll(null!));
    }

    [Fact]
    public void Scroll_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().Scroll(ValidScroll(), Cancelled));
    }

    [Fact]
    public void Scroll_InvalidRequest_ThrowsBeforeNative()
    {
        ScrollRequest forged = ValidScroll() with { Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Scroll(forged));
    }

    // Retrieve triplet (Validate has no cap arm: the invalid leg is a null element).

    [Fact]
    public void Retrieve_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Retrieve(null!));
    }

    [Fact]
    public void Retrieve_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(() => GuardOnlyShard().Retrieve(ValidRetrieve(), Cancelled));
    }

    [Fact]
    public void Retrieve_NullElement_ThrowsArgumentNull()
    {
        var request = new RetrieveRequest { PointIds = [null!] };
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Retrieve(request));
    }

    // Count triplet (Validate only checks the filter: the invalid leg is a predicateless condition).

    [Fact]
    public void Count_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Count(null!));
    }

    [Fact]
    public void Count_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(
            () => GuardOnlyShard().Count(new CountRequest(), Cancelled));
    }

    [Fact]
    public void Count_InvalidFilter_ThrowsBeforeNative()
    {
        var request = new CountRequest
        {
            Filter = new Filter { Must = [Condition.Field(new FieldCondition { Key = "x" })] },
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Count(request));
    }

    // Facet triplet.

    [Fact]
    public void Facet_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().Facet(null!));
    }

    [Fact]
    public void Facet_Cancelled_ThrowsOperationCanceled()
    {
        Assert.Throws<OperationCanceledException>(
            () => GuardOnlyShard().Facet(new FacetRequest { Key = "city" }, Cancelled));
    }

    [Fact]
    public void Facet_InvalidRequest_ThrowsBeforeNative()
    {
        var forged = new FacetRequest { Key = "city", Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Facet(forged));
    }

    // Async: null and pre-cancelled throw synchronously; success matches the sync path.

    [Fact]
    public void QueryAsync_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuardOnlyShard().QueryAsync(null!); });
    }

    [Fact]
    public void QueryAsync_CancelledToken_ThrowsSynchronouslyBeforeTaskRun()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = GuardOnlyShard().QueryAsync(ValidQuery(), source.Token); });
    }

    [Fact]
    public async Task QueryAsync_Success_MatchesSync()
    {
        if (!NativeLoader.IsAvailable)
        {
            await Task.CompletedTask;
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f),
            TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f),
        ]));
        QueryRequest request = ValidQuery();
        IReadOnlyList<ScoredPoint> sync = shard.Query(request);
        IReadOnlyList<ScoredPoint> async = await shard.QueryAsync(request);
        Assert.Equal(sync.Count, async.Count);
        Assert.Equal(sync.Select(p => p.Id), async.Select(p => p.Id));
    }

    [Fact]
    public void SearchAsync_NullRequest_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = GuardOnlyShard().SearchAsync(null!); });
    }

    [Fact]
    public void SearchAsync_CancelledToken_ThrowsSynchronouslyBeforeTaskRun()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => { _ = GuardOnlyShard().SearchAsync(ValidSearch(), source.Token); });
    }

    [Fact]
    public async Task SearchAsync_Success_MatchesSync()
    {
        if (!NativeLoader.IsAvailable)
        {
            await Task.CompletedTask;
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f),
            TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f),
        ]));
        SearchRequest request = ValidSearch();
        IReadOnlyList<ScoredPoint> sync = shard.Search(request);
        IReadOnlyList<ScoredPoint> async = await shard.SearchAsync(request);
        Assert.Equal(sync.Count, async.Count);
        Assert.Equal(sync.Select(p => p.Id), async.Select(p => p.Id));
    }

    // SerializeRequest mapping: JsonException vs depth-limit arms, plus the unmapped NaN case.

    [Fact]
    public void SerializeRequest_DeepPayload_MapsJsonExceptionToInvalidArgument()
    {
        JsonNode node = new JsonObject();
        for (int i = 0; i < 300; i++)
        {
            node = new JsonObject { ["a"] = node };
        }

        Point point = TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with { Payload = (JsonObject)node };
        UpdateOperation operation = UpdateOperation.UpsertPoints([point]);
        EdgeInvalidArgumentException exception =
            Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Update(operation));
        Assert.StartsWith("request serialization failed", exception.Message);
    }

    [Fact]
    public void SerializeRequest_DeepPrefetch_MapsDepthLimitToInvalidArgument()
    {
        Prefetch chain = new() { Limit = 10 };
        for (int i = 0; i < 300; i++)
        {
            chain = new Prefetch { Limit = 10, Prefetches = [chain] };
        }

        MethodInfo? method = typeof(EdgeShard).GetMethod(
            "SerializeRequest", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        TargetInvocationException wrapper = Assert.Throws<TargetInvocationException>(
            () => method!.MakeGenericMethod(typeof(Prefetch)).Invoke(null, [chain, EdgeJsonContext.Deep.Prefetch]));
        EdgeInvalidArgumentException exception =
            Assert.IsType<EdgeInvalidArgumentException>(wrapper.InnerException);
        Assert.StartsWith("request exceeds JSON depth limit", exception.Message);
    }

    [Fact]
    public void SerializeRequest_NaNStartFrom_SurfacesArgumentException()
    {
        // System.Text.Json rejects non-finite doubles with ArgumentException, which neither
        // SerializeRequest catch covers — so it propagates unmapped. ScrollRequest.Validate
        // never inspects OrderBy, letting the value reach serialization.
        var request = new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy { Key = "x", StartFrom = new FloatStartFrom(double.NaN) },
        };
        Assert.Throws<ArgumentException>(() => GuardOnlyShard().Scroll(request));
    }

    // ThrowForStatus mapping and the native-absent ReadLastError path.

    [Fact]
    public void ThrowForStatus_MissingDirectory_ReportsReason()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = Path.Combine(TestHelpers.TempDir(), "nope");
        EdgeOperationException exception = Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(dir));
        Assert.False(string.IsNullOrEmpty(exception.Reason));
        Assert.Contains(dir, exception.Reason);
        Assert.Equal(exception.Reason, exception.Message);
    }

    [Fact]
    public void ThrowForStatus_OccupiedDirectory_MapsOperationError()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard first = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        first.Unload();
        first.Dispose();
        EdgeOperationException exception = Assert.Throws<EdgeOperationException>(
            () => EdgeShard.Create(dir, TestHelpers.FourDimDotConfig()));
        Assert.False(string.IsNullOrEmpty(exception.Reason));
        Assert.Equal(exception.Reason, exception.Message);
    }

    [Fact]
    public void ClientPreCheck_OverCapRequest_ThrowsBeforeNative()
    {
        // The forged limit skips no validation: shard.Search validates before dispatch,
        // so the client pre-check wins over any native OperationError — without native present.
        SearchRequest forged = ValidSearch() with { Limit = EdgeValidation.MaxResultCount + 1 };
        Assert.Throws<EdgeInvalidArgumentException>(() => GuardOnlyShard().Search(forged));
    }

    [Fact]
    public void ReadLastError_NativeAbsent_ReportsUnavailable()
    {
        if (NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeOperationException exception =
            Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(TestHelpers.TempDir()));
        Assert.Contains("not available", exception.Reason);
    }

    // Flush empty-response early return, Optimize booleans, GetConfig snapshot.

    [Fact]
    public void Flush_OnHealthyShard_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(ValidUpdate());
        shard.Flush();
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Optimize_ReturnsFalseWhenIdle()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        Assert.False(shard.Optimize());
    }

    [Fact]
    public void Optimize_ReturnsTrueAfterHeavyDeletes()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.SetOptimizersConfig(new OptimizersConfig { MaxSegmentSizeKb = 1, IndexingThresholdKb = 1 });
        List<Point> points = [];
        for (ulong i = 1; i <= 2000; i++)
        {
            float v = (float)i;
            points.Add(TestHelpers.FourDimPoint(i, v, v + 1, v + 2, v + 3));
        }

        shard.Update(UpdateOperation.UpsertPoints(points));
        shard.Flush();
        Assert.True(shard.Optimize());
        Assert.Equal(2000ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void GetConfig_ReturnsSnapshot()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        EdgeConfig config = shard.GetConfig();
        Assert.NotNull(config);
        Assert.Equal(4ul, config.VectorData[""].Size);
        Assert.Equal(Distance.Dot, config.VectorData[""].Distance);
    }

    // Snapshot manifest: valid JSON probe that survives Flush (unload arm is in the matrix).

    [Fact]
    public void SnapshotManifest_SurvivesFlush()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(ValidUpdate());
        JsonNode? before = JsonNode.Parse(shard.SnapshotManifest());
        shard.Flush();
        JsonNode? after = JsonNode.Parse(shard.SnapshotManifest());
        Assert.IsType<JsonObject>(before);
        Assert.IsType<JsonObject>(after);
        Assert.Contains("segment_id", before!.ToJsonString());
        Assert.Contains("segment_id", after!.ToJsonString());
    }

    // UpdateFromSnapshot guards plus fabricated-archive round trips (the CABI exposes no
    // snapshot-create op, so the archive is a GNU tar of a flushed shard directory).

    [Fact]
    public void UpdateFromSnapshot_NullPath_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => GuardOnlyShard().UpdateFromSnapshot(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UpdateFromSnapshot_WhitespacePath_ThrowsArgument(string path)
    {
        Assert.Throws<ArgumentException>(() => GuardOnlyShard().UpdateFromSnapshot(path));
    }

    [Fact]
    public void UpdateFromSnapshot_WhitespaceTmpDir_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() =>
            GuardOnlyShard().UpdateFromSnapshot(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "  "));
    }

    [Fact]
    public void UpdateFromSnapshot_NullTmpDir_PassesGuard()
    {
        // Guards accept a null tmp dir, so dispatch is reached and the unloaded
        // guard-only handle reports ShardClosed rather than an argument error.
        Assert.Throws<ShardClosedException>(() =>
            GuardOnlyShard().UpdateFromSnapshot(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), null));
    }

    private static void PackShardDirectory(string sourceDir, string tarPath)
    {
        using FileStream output = File.Create(tarPath);
        using var writer = new TarWriter(output, TarEntryFormat.Gnu, leaveOpen: false);
        foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var entry = new GnuTarEntry(TarEntryType.RegularFile, Path.GetRelativePath(sourceDir, file));
            using FileStream data = File.OpenRead(file);
            entry.DataStream = data;
            writer.WriteEntry(entry);
        }
    }

    private static string SnapshotTarOfTwoPoints()
    {
        string dir = TestHelpers.TempDir();
        EdgeShard seed = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            seed.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f),
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f),
            ]));
            seed.Flush();
            seed.Unload();
        }
        finally
        {
            seed.Dispose();
        }

        string tar = Path.Combine(Path.GetTempPath(), "qdrant-edge-snap-" + Guid.NewGuid().ToString("N") + ".tar");
        PackShardDirectory(dir, tar);
        return tar;
    }

    [Fact]
    public void UpdateFromSnapshot_ValidArchive_RestoresPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string tar = SnapshotTarOfTwoPoints();
        try
        {
            using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
            shard.Update(UpdateOperation.UpsertPoints(
                [TestHelpers.FourDimPoint(9, 9f, 9f, 9f, 9f)]));
            shard.UpdateFromSnapshot(tar);
            Assert.Equal(2ul, shard.Count(new CountRequest()));
        }
        finally
        {
            File.Delete(tar);
        }
    }

    [Fact]
    public void UpdateFromSnapshot_TmpDirProvided_Succeeds()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string tar = SnapshotTarOfTwoPoints();
        try
        {
            using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
            shard.Update(UpdateOperation.UpsertPoints(
                [TestHelpers.FourDimPoint(9, 9f, 9f, 9f, 9f)]));
            shard.UpdateFromSnapshot(tar, TestHelpers.TempDir());
            Assert.Equal(2ul, shard.Count(new CountRequest()));
        }
        finally
        {
            File.Delete(tar);
        }
    }

    // UnpackSnapshot guards (they run before EnsureLoaded, so no native is needed).

    [Fact]
    public void UnpackSnapshot_NullSnapshot_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeShard.UnpackSnapshot(null!, TestHelpers.TempDir()));
    }

    [Fact]
    public void UnpackSnapshot_NullTarget_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EdgeShard.UnpackSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UnpackSnapshot_WhitespaceSnapshot_ThrowsArgument(string snapshot)
    {
        Assert.Throws<ArgumentException>(() => EdgeShard.UnpackSnapshot(snapshot, TestHelpers.TempDir()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UnpackSnapshot_WhitespaceTarget_ThrowsArgument(string target)
    {
        Assert.Throws<ArgumentException>(() =>
            EdgeShard.UnpackSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), target));
    }

    [Fact]
    public void UnpackSnapshot_ManifestLessArchive_ThrowsOperationError()
    {
        // A plain tar extracts but carries no snapshot manifest, so the engine rejects it:
        // unpack validates snapshot shape, not just tar readability. A manifest-bearing
        // archive cannot be produced through the CABI (no snapshot-create op), so the
        // success path stays engine-side only.
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string tiny = Path.Combine(Path.GetTempPath(), "qdrant-edge-tiny-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tiny, "hello");
        string tar = Path.Combine(Path.GetTempPath(), "qdrant-edge-tiny-" + Guid.NewGuid().ToString("N") + ".tar");
        try
        {
            using (FileStream output = File.Create(tar))
            using (var writer = new TarWriter(output, TarEntryFormat.Gnu, leaveOpen: false))
            {
                var entry = new GnuTarEntry(TarEntryType.RegularFile, Path.GetFileName(tiny));
                using FileStream data = File.OpenRead(tiny);
                entry.DataStream = data;
                writer.WriteEntry(entry);
            }

            Assert.Throws<EdgeOperationException>(() => EdgeShard.UnpackSnapshot(tar, TestHelpers.TempDir()));
        }
        finally
        {
            File.Delete(tiny);
            File.Delete(tar);
        }
    }

    // Occupied-create fails but the data stays loadable.

    [Fact]
    public void Create_OverOccupiedThenLoad_Succeeds()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard first = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        first.Unload();
        first.Dispose();
        Assert.Throws<EdgeOperationException>(() => EdgeShard.Create(dir, TestHelpers.FourDimDotConfig()));
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(dir, reloaded.Path);
        Assert.Equal(0ul, reloaded.Count(new CountRequest()));
    }
}
