using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>test.md §16 (SearchMatrix) + §19 (native behavior gaps).</summary>
public sealed class NativeBehaviorGapsTests
{
    private const ulong Cap = EdgeValidation.MaxResultCount;

    // Shared fixtures: 3-5 points, 4-dim vectors throughout.

    private static EdgeShard SeedThree(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(2, 0.5f, 0f, 0f, 0f),
                TestHelpers.FourDimPoint(3, 0f, 1f, 0f, 0f),
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static QueryRequest NearestQuery(float a, float b, float c, float d, ulong limit) => new()
    {
        Limit = limit,
        Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([a, b, c, d]))),
    };

    private static SearchRequest NearestSearch(float a, float b, float c, float d, ulong limit) => new()
    {
        Query = Query.Nearest(NamedVector.Dense([a, b, c, d])),
        Limit = limit,
    };

    private static EdgeShard CreateTextShard(TextIndexParams indexParams, IReadOnlyList<(ulong Id, string Title)> docs, out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndexWithParams("title", PayloadIndexParams.Text(indexParams)));
            List<Point> points = [];
            foreach ((ulong id, string title) in docs)
            {
                float v = (float)id;
                points.Add(TestHelpers.FourDimPoint(id, v, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["title"] = title },
                });
            }

            shard.Update(UpdateOperation.UpsertPoints(points));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    private static Filter TitleIs(string text) => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = "title", Match = Match.CreateText(text) })],
    };

    private static ulong CountText(EdgeShard shard, string text) =>
        shard.Count(new CountRequest { Filter = TitleIs(text) });

    // §16 SearchMatrix: validation arms.

#pragma warning disable QDRANT_EDGE_MATRIX
    [Fact]
    public void SearchMatrix_NullRequest_Throws()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<ArgumentNullException>(() => shard.SearchMatrix(null!));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_Cancelled_ThrowsBeforeNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        SearchMatrixRequest request = new() { SampleSize = 2, LimitPerSample = 1 };
        Assert.Throws<OperationCanceledException>(() => shard.SearchMatrix(request, new CancellationToken(true)));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_OverCap_ThrowsBeforeNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        SearchMatrixRequest request = new() { SampleSize = Cap + 1, LimitPerSample = 1 };
        EdgeInvalidArgumentException exception = Assert.Throws<EdgeInvalidArgumentException>(() => shard.SearchMatrix(request));
        Assert.Contains("sample_size", exception.Reason);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_SmallShard_ReturnsSamplesAndNearests()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
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

    [Fact]
    public void SearchMatrix_Filtered_ReturnsSubset()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 2,
            LimitPerSample = 1,
            Filter = EdgeShardReadsNativeTests.CategoryIs("a"),
        });
        Assert.Equal(2, response.SampleIds.Count);
        Assert.All(response.SampleIds, id =>
        {
            ulong value = ((NumPointId)id).Value;
            Assert.True(value is 1 or 2);
        });
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_UsingNamedVector_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
                ["image"] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([1f, 0f, 0f, 0f]),
                    ["image"] = NamedVector.Dense([0f, 1f, 0f, 0f]),
                }),
            },
            TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([0f, 1f, 0f, 0f]),
                    ["image"] = NamedVector.Dense([1f, 0f, 0f, 0f]),
                }),
            },
            TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with
            {
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    [""] = NamedVector.Dense([0f, 0f, 1f, 0f]),
                    ["image"] = NamedVector.Dense([0f, 0f, 1f, 1f]),
                }),
            },
        ]));
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 2,
            LimitPerSample = 1,
            Using = "image",
        });
        Assert.Equal(2, response.SampleIds.Count);
        Assert.Equal(2, response.Nearests.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_EmptyShard_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 2,
            LimitPerSample = 1,
        });
        Assert.Empty(response.SampleIds);
        Assert.Empty(response.Nearests);
    }

    [Fact]
    public void SearchMatrix_AfterUnload_ThrowsShardClosed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = SeedThree(out _);
        try
        {
            shard.Unload();
            SearchMatrixRequest request = new() { SampleSize = 2, LimitPerSample = 1 };
            Assert.Throws<ShardClosedException>(() => shard.SearchMatrix(request));
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Fact]
    public void SearchMatrix_LimitAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = Cap,
            LimitPerSample = 1,
        });
        Assert.Equal(3, response.SampleIds.Count);
        Assert.Equal(3, response.Nearests.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_ZeroSampleSize_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 0,
            LimitPerSample = 1,
        });
        Assert.Empty(response.SampleIds);
        Assert.Empty(response.Nearests);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void SearchMatrix_ZeroLimitPerSample_ReturnsEmptyNearests()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        SearchMatrixResponse response = shard.SearchMatrix(new SearchMatrixRequest
        {
            SampleSize = 2,
            LimitPerSample = 0,
        });
        Assert.Empty(response.SampleIds);
        Assert.Empty(response.Nearests);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }
#pragma warning restore QDRANT_EDGE_MATRIX

    [Fact]
    public void SearchMatrixResponse_NumAndUuidIds_RoundTrips()
    {
        Guid uuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        SearchMatrixResponse response = new()
        {
            SampleIds = [PointId.FromNumber(1), PointId.FromUuid(uuid)],
            Nearests =
            [
                [new ScoredPoint { Id = PointId.FromNumber(2), Version = 1, Score = 0.5f }],
                [new ScoredPoint { Id = PointId.FromUuid(uuid), Version = 2, Score = 1.5f }],
            ],
        };
        string json = JsonSerializer.Serialize(response, EdgeJsonContext.Deep.SearchMatrixResponse);
        SearchMatrixResponse? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SearchMatrixResponse);
        Assert.NotNull(back);
        Assert.Equal(response.SampleIds, back.SampleIds);
        Assert.Equal(2, back.Nearests.Count);
        Assert.Equal(PointId.FromNumber(2), back.Nearests[0][0].Id);
        Assert.Equal(PointId.FromUuid(uuid), back.Nearests[1][0].Id);
        Assert.Equal(0.5f, back.Nearests[0][0].Score);
    }

    // §19 limit-at-cap: the client pre-check passes at the cap, so the engine must not abort.

    [Fact]
    public void Search_LimitAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Search(NearestSearch(1f, 0f, 0f, 0f, Cap));
        Assert.Equal(3, hits.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Query_LimitAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, Cap));
        Assert.Equal(3, hits.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Scroll_LimitAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = Cap });
        Assert.Equal(3, page.Records.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Facet_LimitAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        shard.Update(UpdateOperation.CreateFieldIndex("category", PayloadSchemaType.Keyword));
        FacetResponse response = shard.Facet(new FacetRequest { Key = "category", Limit = Cap });
        Assert.Equal(2, response.Hits.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void QueryGroups_GroupsAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10),
            GroupBy = "category",
            Groups = Cap,
            GroupSize = 1,
        });
        Assert.Equal(2, groups.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Mmr_CandidatesAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 3,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 0f, 0f, 0f]), 0.5f, Cap),
        });
        Assert.Equal(3, hits.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 over-cap: unreachable via public API, the client pre-check fires first.

    [Fact]
    public void Search_LimitAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Search(NearestSearch(1f, 0f, 0f, 0f, Cap + 1)));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Query_LimitAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Query(NearestQuery(1f, 0f, 0f, 0f, Cap + 1)));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Scroll_LimitAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Scroll(new ScrollRequest { Limit = Cap + 1 }));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Facet_LimitAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Facet(new FacetRequest { Key = "category", Limit = Cap + 1 }));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void QueryGroups_GroupsAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        GroupRequest request = new()
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10),
            GroupBy = "category",
            Groups = Cap + 1,
            GroupSize = 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.QueryGroups(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void QueryGroups_GroupSizeAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        GroupRequest request = new()
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10),
            GroupBy = "category",
            Groups = 10,
            GroupSize = Cap + 1,
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.QueryGroups(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void QueryGroups_GroupSizeAtCap_DoesNotAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10),
            GroupBy = "category",
            Groups = 2,
            GroupSize = Cap,
        });
        Assert.Equal(2, groups.Count);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Prefetch_LimitAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        QueryRequest request = new()
        {
            Limit = 10,
            Prefetches = [new Prefetch { Limit = Cap + 1, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) }],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Query(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Mmr_CandidatesAboveCap_ThrowsInvalidArgument()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        QueryRequest request = new()
        {
            Limit = 3,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 0f, 0f, 0f]), 0.5f, Cap + 1),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Query(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 HNSW boundaries.

    [Fact]
    public void Hnsw_M2048_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig
                {
                    Size = 4,
                    Distance = Distance.Dot,
                    HnswConfig = new HnswIndexConfig { M = 2048 },
                },
            },
        };
        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), config);
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f)]));
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Hnsw_M2049_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.SetHnswConfig(new HnswIndexConfig { M = 2049 }));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Hnsw_EfConstruct4_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.SetHnswConfig(new HnswIndexConfig { M = 16, EfConstruct = 4 });
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Hnsw_EfConstruct100000_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.SetHnswConfig(new HnswIndexConfig { M = 16, EfConstruct = 100_000 });
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Hnsw_MaxIndexingThreads1024_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.SetHnswConfig(new HnswIndexConfig { M = 16, EfConstruct = 100, MaxIndexingThreads = 1024 });
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 vector size boundaries.

    [Fact]
    public void VectorSize_One_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 1, Distance = Distance.Dot },
            },
        };
        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point { Id = PointId.FromNumber(1), Vector = Vector.Single([2f]) },
            new Point { Id = PointId.FromNumber(2), Vector = Vector.Single([1f]) },
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f]))),
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2)], [hits[0].Id, hits[1].Id]);
    }

    [Fact]
    public void VectorSize_Zero_Rejected()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 0, Distance = Distance.Dot },
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => EdgeShard.Create(TestHelpers.TempDir(), config));
    }

    [Fact]
    public void Upsert_EmptyDense_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Point bad = new() { Id = PointId.FromNumber(9), Vector = Vector.Single([]) };
        UpdateOperation operation = new UpsertPointsOperation([bad], null, null);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Update(operation));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void VectorSize_65536_Accepted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 65536, Distance = Distance.Dot },
            },
        };
        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), config);
        float[] values = new float[65536];
        values[0] = 1f;
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point { Id = PointId.FromNumber(1), Vector = Vector.Single(values) },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    // §19 distance metrics beyond Dot/Cosine.

    [Fact]
    public void Distance_Euclid_RanksByEuclideanDistance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Euclid },
            },
        };
        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 2f, 0f, 0f, 0f),
            TestHelpers.FourDimPoint(2, 1f, 0f, 0f, 0f),
            TestHelpers.FourDimPoint(3, 0f, 1f, 0f, 0f),
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, 10));
        Assert.Equal([PointId.FromNumber(2), PointId.FromNumber(1), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void Distance_Manhattan_RanksByManhattanDistance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Manhattan },
            },
        };
        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 2f, 1f, 0f, 0f),
            TestHelpers.FourDimPoint(2, 1.7f, 1.7f, 0f, 0f),
            TestHelpers.FourDimPoint(3, 1f, 3f, 0f, 0f),
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 1f, 0f, 0f, 10));
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    // §19 multi-dense engine-boundary rejects (client pre-check fires first).

    [Fact]
    public void MultiDense_Empty_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Point bad = new() { Id = PointId.FromNumber(9), Vector = new MultiDenseVector([]) };
        UpdateOperation operation = new UpsertPointsOperation([bad], null, null);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Update(operation));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void MultiDense_Ragged_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Point bad = new() { Id = PointId.FromNumber(9), Vector = new MultiDenseVector([[1f, 2f], [3f]]) };
        UpdateOperation operation = new UpsertPointsOperation([bad], null, null);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Update(operation));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void MultiDense_ZeroDimRow_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Point bad = new() { Id = PointId.FromNumber(9), Vector = new MultiDenseVector([[]]) };
        UpdateOperation operation = new UpsertPointsOperation([bad], null, null);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Update(operation));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 sparse: Idf modifier search + empty sparse behavior.

    [Fact]
    public void Sparse_Idf_ReturnsHits()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
            {
                ["text"] = new SparseVectorDataConfig { Modifier = Modifier.Idf },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point
            {
                Id = PointId.FromNumber(1),
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    ["text"] = NamedVector.Sparse(new SparseVector { Indices = [1, 5], Values = [1f, 2f] }),
                }),
            },
            new Point
            {
                Id = PointId.FromNumber(2),
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    ["text"] = NamedVector.Sparse(new SparseVector { Indices = [9], Values = [1f] }),
                }),
            },
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Nearest(
                NamedVector.Sparse(new SparseVector { Indices = [1], Values = [1f] }),
                "text")),
        });
        Assert.NotEmpty(hits);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void Sparse_Empty_UpsertAcceptedAndCounted()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            SparseVectorData = new Dictionary<string, SparseVectorDataConfig>
            {
                ["text"] = new SparseVectorDataConfig(),
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point
            {
                Id = PointId.FromNumber(1),
                Vector = Vector.Named(new Dictionary<string, NamedVector>
                {
                    ["text"] = NamedVector.Sparse(new SparseVector { Indices = [], Values = [] }),
                }),
            },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest()));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(true),
        });
        Assert.Single(records);
    }

    // §19 UUID ids round-trip over read paths.

    [Fact]
    public void UuidIds_RoundTripThroughReads()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        Guid first = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid second = Guid.Parse("66666666-7777-8888-9999-000000000000");
        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point { Id = PointId.FromUuid(first), Vector = Vector.Single([1f, 0f, 0f, 0f]) },
            new Point { Id = PointId.FromUuid(second), Vector = Vector.Single([0f, 1f, 0f, 0f]) },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromUuid(first), PointId.FromUuid(second), PointId.FromNumber(1)],
        });
        Assert.Equal(3, records.Count);
        Assert.Contains(PointId.FromUuid(first), records.Select(record => record.Id));
        ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 10 });
        Assert.Equal(5, page.Records.Count);
        Assert.Contains(PointId.FromUuid(second), page.Records.Select(record => record.Id));
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, 10));
        Assert.Equal(PointId.FromUuid(first), hits[0].Id);
    }

    [Fact]
    public void HasId_UuidIds_CountsMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        Guid first = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid second = Guid.Parse("66666666-7777-8888-9999-000000000000");
        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point { Id = PointId.FromUuid(first), Vector = Vector.Single([1f, 0f, 0f, 0f]) },
            new Point { Id = PointId.FromUuid(second), Vector = Vector.Single([0f, 1f, 0f, 0f]) },
        ]));
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.HasId([PointId.FromUuid(first), PointId.FromUuid(second)])] },
        }));
    }

    [Fact]
    public void UuidIds_FromString_RoundTripsThroughReads()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        PointId parsed = PointId.FromString("11111111-2222-3333-4444-555555555555");
        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point { Id = parsed, Vector = Vector.Single([1f, 0f, 0f, 0f]) },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [parsed],
        });
        Record single = Assert.Single(records);
        Assert.Equal(parsed, single.Id);
    }

    // §19 payload shapes + NaN rejection.

    [Fact]
    public void Payload_Nested_RoundTrips()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 1f, 0f, 0f) with
            {
                Payload = new JsonObject
                {
                    ["outer"] = new JsonObject { ["inner"] = "deep" },
                    ["flag"] = true,
                },
            },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(4)],
        });
        Record single = Assert.Single(records);
        JsonObject payload = (JsonObject)JsonNode.Parse(single.Payload!)!;
        Assert.Equal("deep", payload["outer"]!["inner"]!.GetValue<string>());
        Assert.True(payload["flag"]!.GetValue<bool>());
    }

    [Fact]
    public void Payload_BoolIntFloatNull_RoundTrips()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 1f, 0f, 0f) with
            {
                Payload = new JsonObject
                {
                    ["b"] = false,
                    ["i"] = -7,
                    ["f"] = 2.5,
                    ["n"] = null,
                },
            },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(4)],
        });
        Record single = Assert.Single(records);
        JsonObject payload = (JsonObject)JsonNode.Parse(single.Payload!)!;
        Assert.False(payload["b"]!.GetValue<bool>());
        Assert.Equal(-7, payload["i"]!.GetValue<int>());
        Assert.Equal(2.5, payload["f"]!.GetValue<double>());
        Assert.True(payload.ContainsKey("n"));
    }

    [Fact]
    public void Payload_NaN_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.ThrowsAny<EdgeException>(() =>
            shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"x": NaN}""")));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Payload_PosInf_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.ThrowsAny<EdgeException>(() =>
            shard.Update(UpdateOperation.SetPayload([PointId.FromNumber(1)], """{"x": Infinity}""")));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 text-index knobs: one Count per knob.

    [Fact]
    public void TextIndex_TokenizerWord_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Tokenizer = TokenizerType.Word }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_TokenizerWhitespace_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Tokenizer = TokenizerType.Whitespace }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_TokenizerMultilingual_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Tokenizer = TokenizerType.Multilingual }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_TokenizerPrefix_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Tokenizer = TokenizerType.Prefix }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_MinTokenLen_FiltersShortTokens()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { MinTokenLen = 10 }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(0ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_MaxTokenLen_FiltersLongTokens()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { MaxTokenLen = 3 }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(0ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_LowercaseDisabled_IsCaseSensitive()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Lowercase = false }, [(1, "Hello"), (2, "Goodbye")], out _);
        Assert.Equal(0ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_AsciiFolding_MatchesFolded()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { AsciiFolding = true }, [(1, "caf\u00e9"), (2, "tea")], out _);
        Assert.Equal(1ul, CountText(shard, "cafe"));
    }

    [Fact]
    public void TextIndex_PhraseMatching_MatchesPhrase()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { PhraseMatching = true }, [(1, "quick brown fox"), (2, "lazy dog")], out _);
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "title", Match = Match.CreatePhrase("quick brown") })],
            },
        }));
    }

    [Fact]
    public void TextIndex_SnowballStemmer_MatchesStem()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(
            new TextIndexParams { Stemmer = Stemmer.Snowball(SnowballLanguage.English) },
            [(1, "running fast"), (2, "lazy dog")],
            out _);
        Assert.Equal(1ul, CountText(shard, "run"));
    }

    [Fact]
    public void TextIndex_DisabledStemmer_MatchesLiterally()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(
            new TextIndexParams { Stemmer = Stemmer.Disabled() },
            [(1, "running fast"), (2, "lazy dog")],
            out _);
        Assert.Equal(0ul, CountText(shard, "run"));
    }

    [Fact]
    public void TextIndex_MemoryCached_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { Memory = Memory.Cached }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    [Fact]
    public void TextIndex_EnableHnsw_Matches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CreateTextShard(new TextIndexParams { EnableHnsw = true }, [(1, "hello world"), (2, "goodbye moon")], out _);
        Assert.Equal(1ul, CountText(shard, "hello"));
    }

    // §19 integer index lookup vs range.

    [Fact]
    public void IntegerIndex_LookupOnly_MatchesExactly()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "n", PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = true, Range = false })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 1 } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 2 } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["n"] = 3 } },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "n", Match = Match.CreateValue(ValueVariants.Integer(2)) })],
            },
        }));
    }

    [Fact]
    public void IntegerIndex_RangeOnly_FiltersByRange()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "n", PayloadIndexParams.Integer(new IntegerIndexParams { Lookup = false, Range = true })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 1 } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 2 } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["n"] = 3 } },
        ]));
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "n", Range = new RangeFloat { Gte = 2 } })],
            },
        }));
    }

    // §19 read-path filter pins.

    [Fact]
    public void DatetimeRange_FiltersByBounds()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.CreateFieldIndex("ts", PayloadSchemaType.Datetime));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-01-10T00:00:00Z" } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-02-10T00:00:00Z" } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-03-10T00:00:00Z" } },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition
                {
                    Key = "ts",
                    DatetimeRange = new RangeDatetime { Gte = "2024-02-01T00:00:00Z", Lte = "2024-03-01T00:00:00Z" },
                })],
            },
        }));
    }

    [Fact]
    public void ValuesCount_FiltersByArrayLength()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["tags"] = new JsonArray(1) } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["tags"] = new JsonArray(1, 2) } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["tags"] = new JsonArray(1, 2, 3) } },
        ]));
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "tags", ValuesCount = new ValuesCount { Gte = 2 } })],
            },
        }));
    }

    [Fact]
    public void IsEmpty_MatchesMissingKey()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject() },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["opt"] = "x" } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject() },
        ]));
        // Seed points carry no payload at all, so all but point 5 match.
        Assert.Equal(5ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.IsEmpty("opt")] },
        }));
    }

    [Fact]
    public void IsNull_MatchesExplicitNull()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["k"] = null } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["k"] = "v" } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject() },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.IsNull("k")] },
        }));
    }

    [Fact]
    public void HasId_CountsMatchingIds()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.HasId([PointId.FromNumber(1), PointId.FromNumber(3)])] },
        }));
    }

    [Fact]
    public void HasId_Query_ReturnsMatchingPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        QueryRequest request = NearestQuery(1f, 0f, 0f, 0f, 10) with
        {
            Filter = new Filter { Must = [Condition.HasId([PointId.FromNumber(1), PointId.FromNumber(3)])] },
        };
        IReadOnlyList<ScoredPoint> hits = shard.Query(request);
        Assert.Equal(2, hits.Count);
        Assert.Contains(PointId.FromNumber(1), hits.Select(hit => hit.Id));
        Assert.Contains(PointId.FromNumber(3), hits.Select(hit => hit.Id));
    }

    [Fact]
    public void HasVector_FiltersPointsMissingNamedVector()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = new()
        {
            VectorData = new Dictionary<string, VectorDataConfig>
            {
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
                ["vec2"] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        static Point TwoVectorPoint(ulong id) => TestHelpers.FourDimPoint(id, 1f, 2f, 3f, 4f) with
        {
            Vector = Vector.Named(new Dictionary<string, NamedVector>
            {
                [""] = NamedVector.Dense([1f, 2f, 3f, 4f]),
                ["vec2"] = NamedVector.Dense([5f, 6f, 7f, 8f]),
            }),
        };
        shard.Update(UpdateOperation.UpsertPoints([TwoVectorPoint(1), TwoVectorPoint(2)]));
        shard.Update(UpdateOperation.DeleteVectors([PointId.FromNumber(1)], ["vec2"]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.HasVector("vec2")] },
        }));
    }

    [Fact]
    public void Slice_PartitionsPointsComplementarily()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(4, 1f, 1f, 1f, 1f)]));
        ulong first = shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.Slice(2, 0)] },
        });
        ulong second = shard.Count(new CountRequest
        {
            Filter = new Filter { Must = [Condition.Slice(2, 1)] },
        });
        // Id-hash slicing assigns three points to slice 0 and one to slice 1.
        Assert.Equal(3ul, first);
        Assert.Equal(1ul, second);
    }

    [Fact]
    public void Nested_FiltersInsideObject()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with
            {
                Payload = new JsonObject { ["outer"] = new JsonArray(new JsonObject { ["inner"] = "x" }) },
            },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with
            {
                Payload = new JsonObject { ["outer"] = new JsonArray(new JsonObject { ["inner"] = "y" }) },
            },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject() },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                // Nested filters match inside arrays of objects.
                Must = [Condition.Nested("outer", new Filter
                {
                    Must = [Condition.Field(new FieldCondition { Key = "inner", Match = Match.CreateValue(ValueVariants.String("x")) })],
                })],
            },
        }));
    }

    [Fact]
    public void MinShould_CountsPointsMeetingThreshold()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        Assert.Equal(3ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                MinShould = new MinShould
                {
                    Conditions =
                    [
                        Condition.Field(new FieldCondition { Key = "category", Match = Match.CreateValue(ValueVariants.String("a")) }),
                        Condition.Field(new FieldCondition { Key = "category", Match = Match.CreateValue(ValueVariants.String("b")) }),
                    ],
                    MinCount = 1,
                },
            },
        }));
    }

    [Fact]
    public void MustNot_ExcludesMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = new Filter { MustNot = [EdgeShardReadsNativeTests.CategoryIs("a").Must![0]] } }));
    }

    [Fact]
    public void Should_MatchesAnyClause()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter { Should = [EdgeShardReadsNativeTests.CategoryIs("a").Must![0]] },
        }));
    }

    // §19 recommend / discover / context / feedback ranking.

    [Fact]
    public void Recommend_SumScores_RanksAllPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Recommend(
                [NamedVector.Dense([1f, 0f, 0f, 0f])],
                [],
                RecommendStrategy.SumScores)),
        });
        Assert.Equal(3, hits.Count);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void Discover_RanksTowardTarget()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Discover(
                NamedVector.Dense([1f, 0f, 0f, 0f]),
                [new ContextPair { Positive = NamedVector.Dense([1f, 0f, 0f, 0f]), Negative = NamedVector.Dense([0f, 1f, 0f, 0f]) }])),
        });
        Assert.Equal(3, hits.Count);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void Context_RanksNegativeLast()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.CreateContext(
                [new ContextPair { Positive = NamedVector.Dense([1f, 0f, 0f, 0f]), Negative = NamedVector.Dense([0f, 1f, 0f, 0f]) }])),
        });
        // Points 1 and 2 both score 0, so their relative order is not pinned;
        // point 3 scores below zero and always sorts last.
        Assert.Equal(3, hits.Count);
        Assert.Equal(PointId.FromNumber(3), hits[2].Id);
        Assert.Equal([1ul, 2ul], hits.Take(2).Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Feedback_RanksTowardTarget()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.CreateFeedback(
                NamedVector.Dense([1f, 0f, 0f, 0f]),
                [new FeedbackItem { Vector = NamedVector.Dense([1f, 0f, 0f, 0f]), Score = 1f }],
                new FeedbackCoefficients { A = 1f, B = 0f, C = 0f })),
        });
        Assert.Equal(3, hits.Count);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void Feedback_NaNCoefficient_RejectedWithoutAbort()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        QueryRequest request = new()
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.CreateFeedback(
                NamedVector.Dense([1f, 0f, 0f, 0f]),
                [new FeedbackItem { Vector = NamedVector.Dense([1f, 0f, 0f, 0f]), Score = 1f }],
                new FeedbackCoefficients { A = float.NaN, B = 0f, C = 0f })),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Query(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 fusion weights + DBSF + K=0.

    [Fact]
    public void Fusion_RrfWeighted_FusesBothSources()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) },
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([0f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2, [3f, 1f])),
        });
        Assert.Equal([1ul, 2ul, 3ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Fusion_Dbsf_FusesBothSources()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) },
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([0f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Dbsf()),
        });
        Assert.Equal([1ul, 2ul, 3ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Fusion_RrfKZero_ThrowsJsonException()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        // Engine gap: RRF with K=0 yields hits with a null score, which
        // violates the ScoredPoint contract (Score is a required float), so
        // deserialization fails. Pin the actual behavior; the shard survives.
        using EdgeShard shard = SeedThree(out _);
        QueryRequest request = new()
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) },
                new Prefetch { Limit = 10, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([0f, 1f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(0)),
        };
        Assert.Throws<JsonException>(() => shard.Query(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 MMR lambda boundaries + candidates 0.

    [Fact]
    public void Mmr_LambdaZero_ReturnsFullSet()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 0f, 0f, 0f]), 0f, 10),
        });
        Assert.Equal([1ul, 2ul, 3ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void Mmr_LambdaOne_RanksByRelevance()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 0f, 0f, 0f]), 1f, 10),
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void Mmr_CandidatesZero_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.Mmr(NamedVector.Dense([1f, 0f, 0f, 0f]), 0.5f, 0),
        });
        Assert.Empty(hits);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 sample query.

    [Fact]
    public void Sample_Random_ReturnsLimitPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 2,
            Query = ScoringQuery.CreateSample(Sample.Random),
        });
        Assert.Equal(2, hits.Count);
    }

    // §19 prefetch clauses.

    [Fact]
    public void Prefetch_WithFilter_RestrictsToMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch
                {
                    Limit = 10,
                    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
                    Filter = EdgeShardReadsNativeTests.CategoryIs("a"),
                },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        });
        Assert.Equal([1ul, 2ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Prefetch_WithScoreThreshold_DropsLowScores()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch
                {
                    Limit = 10,
                    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
                    ScoreThreshold = 0.9f,
                },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        });
        Assert.Equal([PointId.FromNumber(1)], [hits[0].Id]);
        Assert.Single(hits);
    }

    [Fact]
    public void Prefetch_WithParams_ReturnsSameSet()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch
                {
                    Limit = 10,
                    Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
                    Params = new SearchParams { HnswEf = 16 },
                },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        });
        Assert.Equal([1ul, 2ul, 3ul], hits.Select(hit => ((NumPointId)hit.Id).Value).Order());
    }

    [Fact]
    public void Prefetch_NestedTwoLevels_ReturnsResults()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Prefetches =
            [
                new Prefetch
                {
                    Limit = 10,
                    Prefetches =
                    [
                        new Prefetch
                        {
                            Limit = 10,
                            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
                        },
                    ],
                    Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
                },
            ],
            Query = ScoringQuery.CreateFusion(Fusion.Rrf(2)),
        });
        Assert.NotEmpty(hits);
        Assert.Contains(PointId.FromNumber(1), hits.Select(hit => hit.Id));
    }

    // §19 search params.

    [Fact]
    public void SearchParams_Exact_RanksSameOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, 10) with
        {
            Params = new SearchParams { Exact = true },
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void SearchParams_HnswEf_RanksSameOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, 10) with
        {
            Params = new SearchParams { HnswEf = 16 },
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    [Fact]
    public void SearchParams_IndexedOnly_RanksSameOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(NearestQuery(1f, 0f, 0f, 0f, 10) with
        {
            Params = new SearchParams { IndexedOnly = true },
        });
        Assert.Equal([PointId.FromNumber(1), PointId.FromNumber(2), PointId.FromNumber(3)], [hits[0].Id, hits[1].Id, hits[2].Id]);
    }

    // §19 scroll edge cases.

    [Fact]
    public void Scroll_EmptyShard_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 10 });
        Assert.Empty(page.Records);
        Assert.Null(page.NextOffset);
    }

    [Fact]
    public void Scroll_FilterNoMatch_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            Filter = EdgeShardReadsNativeTests.CategoryIs("zzz"),
        });
        Assert.Empty(page.Records);
        Assert.Null(page.NextOffset);
    }

    [Fact]
    public void Scroll_NextOffsetNull_EndsPagination()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        List<ulong> ids = [];
        PointId? offset = null;
        do
        {
            ScrollResponse page = shard.Scroll(new ScrollRequest { Limit = 2, Offset = offset });
            ids.AddRange(page.Records.Select(record => ((NumPointId)record.Id).Value));
            offset = page.NextOffset;
        }
        while (offset is not null);
        Assert.Equal([1ul, 2ul, 3ul], ids.Order());
        ScrollResponse tail = shard.Scroll(new ScrollRequest { Limit = 10 });
        Assert.Null(tail.NextOffset);
    }

    // §19 retrieve edge cases.

    [Fact]
    public void Retrieve_AllMissing_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(998), PointId.FromNumber(999)],
        });
        Assert.Empty(records);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Retrieve_EmptyIds_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest { PointIds = [] });
        Assert.Empty(records);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Retrieve_DuplicateIds_ReturnsSingleRecord()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1), PointId.FromNumber(1)],
        });
        Record single = Assert.Single(records);
        Assert.Equal(PointId.FromNumber(1), single.Id);
    }

    // §19 count exactness.

    [Fact]
    public void Count_ExactFalse_ReturnsFullCount()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Equal(3ul, shard.Count(new CountRequest { Exact = false }));
    }

    [Fact]
    public void Count_ExactTrue_ReturnsFullCount()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Equal(3ul, shard.Count(new CountRequest { Exact = true }));
    }

    // §19 facet variants.

    private static EdgeShard CityColorShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("city", PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.CreateFieldIndex("color", PayloadSchemaType.Keyword));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["city"] = "red", ["color"] = "red" },
                },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["city"] = "red", ["color"] = "blue" },
                },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with
                {
                    Payload = new JsonObject { ["city"] = "blue", ["color"] = "red" },
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

    [Fact]
    public void Facet_Filtered_CountsOnlyMatches()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityColorShard(out _);
        FacetResponse response = shard.Facet(new FacetRequest
        {
            Key = "city",
            Limit = 10,
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "color", Match = Match.CreateValue(ValueVariants.String("red")) })],
            },
        });
        // Points 1 (red) and 3 (blue) have color red: one hit each, tie order unpinned.
        Assert.Equal(2, response.Hits.Count);
        Assert.Equal(["blue", "red"], response.Hits.Select(hit => hit.Value).Order());
        Assert.All(response.Hits, hit => Assert.Equal(1ul, hit.Count));
    }

    [Fact]
    public void Facet_ExactTrue_CountsExactly()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityColorShard(out _);
        FacetResponse response = shard.Facet(new FacetRequest { Key = "city", Limit = 10, Exact = true });
        Assert.Equal(2, response.Hits.Count);
        Assert.Equal(3ul, response.Hits.Select(hit => hit.Count).Aggregate((a, b) => a + b));
    }

    [Fact]
    public void Facet_LimitZero_ReturnsEmpty()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = CityColorShard(out _);
        FacetResponse response = shard.Facet(new FacetRequest { Key = "city", Limit = 0 });
        Assert.Empty(response.Hits);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 batch order with mixed filters.

    [Fact]
    public void QueryBatch_MixedFilters_PreservesInputOrder()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        QueryRequest first = NearestQuery(1f, 0f, 0f, 0f, 10) with { Filter = EdgeShardReadsNativeTests.CategoryIs("a") };
        QueryRequest second = NearestQuery(1f, 0f, 0f, 0f, 1) with { Filter = EdgeShardReadsNativeTests.CategoryIs("b") };
        QueryRequest third = NearestQuery(1f, 0f, 0f, 0f, 10);
        IReadOnlyList<IReadOnlyList<ScoredPoint>> batched = shard.QueryBatch(new QueryBatchRequest
        {
            Queries = [first, second, third],
        });
        Assert.Equal(3, batched.Count);
        Assert.Equal([2, 1, 3], batched.Select(hits => hits.Count));
        Assert.Equal([1ul, 2ul], batched[0].Select(hit => ((NumPointId)hit.Id).Value).Order());
        Assert.Equal(3ul, ((NumPointId)batched[1][0].Id).Value);
    }

    // §19 groups: integer keys + empty groups.

    [Fact]
    public void QueryGroups_IntKeys_GroupsByInteger()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.CreateFieldIndex("n", PayloadSchemaType.Integer));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 1 } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["n"] = 1 } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["n"] = 2 } },
        ]));
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10),
            GroupBy = "n",
            Groups = 10,
            GroupSize = 10,
        });
        Assert.Equal(2, groups.Count);
        // Integer payload keys group under unsigned ids.
        Group ones = groups.Single(group => group.Key.Equals(new UInt64GroupId(1)));
        Assert.Equal(2, ones.Hits.Count);
        Group twos = groups.Single(group => group.Key.Equals(new UInt64GroupId(2)));
        Assert.Single(twos.Hits);
    }

    [Fact]
    public void QueryGroups_FilterNoMatch_ReturnsEmptyGroups()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShardReadsNativeTests.CategorizedShard(out _);
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = NearestQuery(1f, 0f, 0f, 0f, 10) with { Filter = EdgeShardReadsNativeTests.CategoryIs("zzz") },
            GroupBy = "category",
            Groups = 10,
            GroupSize = 1,
        });
        Assert.Empty(groups);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 config-merge tunables: HNSW settings applied before reload keep working after it.

    [Fact]
    public void ConfigMerge_HnswTunable_SurvivesReload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out string dir);
        shard.SetVectorHnswConfig(string.Empty, new HnswIndexConfig { M = 32, EfConstruct = 100 });
        shard.Flush();
        shard.Unload();
        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(3ul, reloaded.Count(new CountRequest()));
        EdgeConfig echoed = reloaded.GetConfig();
        Assert.Equal(4ul, echoed.VectorData[string.Empty].Size);
        Assert.Equal(32ul, echoed.VectorData[string.Empty].HnswConfig?.M);
        reloaded.SetHnswConfig(new HnswIndexConfig { M = 32, EfConstruct = 100 });
        Assert.Equal(3ul, reloaded.Count(new CountRequest()));
    }

    // §19 snapshot manifest contents (no create-snapshot op is exposed; manifest is the pin).

    [Fact]
    public void SnapshotManifest_ContainsSingleSegmentEntry()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        // The manifest is a flat map from segment id to segment entry
        // (segment_id, segment_version, file_versions); file order is not pinned.
        using EdgeShard shard = SeedThree(out _);
        JsonObject manifest = (JsonObject)JsonNode.Parse(shard.SnapshotManifest())!;
        KeyValuePair<string, JsonNode?> entry = Assert.Single(manifest);
        Assert.Equal(entry.Key, entry.Value!["segment_id"]!.GetValue<string>());
        Assert.NotNull(entry.Value!["segment_version"]);
        Assert.IsType<JsonObject>(entry.Value!["file_versions"]);
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 optimize idle vs dirty.

    [Fact]
    public void Optimize_IdleShard_ReturnsFalse()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        Assert.False(shard.Optimize());
    }

    [Fact]
    public void Optimize_SmallShard_ReportsNoWork()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        // Tiny single-segment shards report no optimization work even after
        // writes and deletes; the dirty-true path needs a larger fixture (see below).
        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.DeletePoints([PointId.FromNumber(3)]));
        Assert.False(shard.Optimize());
        Assert.Equal(2ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Optimize_AfterHeavyDeletes_ReturnsTrue()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        List<Point> points = [];
        for (ulong id = 1; id <= 1000; id++)
        {
            points.Add(TestHelpers.FourDimPoint(id, (float)id, 0f, 0f, 1f));
        }

        shard.Update(UpdateOperation.UpsertPoints(points));
        shard.Flush();
        shard.Update(UpdateOperation.DeletePoints(
            Enumerable.Range(1, 900).Select(i => PointId.FromNumber((ulong)i)).ToList()));
        shard.Flush();
        Assert.True(shard.Optimize());
        Assert.Equal(100ul, shard.Count(new CountRequest()));
    }

    // §19 concurrency trio.

    [Fact]
    public async Task ConcurrentCountVsUnload_OnlyOkOrShardClosed()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        EdgeShard shard = SeedThree(out _);
        ConcurrentBag<Exception> errors = [];
        List<Task> tasks = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    _ = shard.Count(new CountRequest());
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        })).ToList();
        shard.Unload();
        await Task.WhenAll(tasks);
        try
        {
            Assert.All(errors, error => Assert.IsType<ShardClosedException>(error));
        }
        finally
        {
            shard.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentFlushUnderUpserts_DoesNotThrow()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        ConcurrentBag<Exception> errors = [];
        Task writer = Task.Run(() =>
        {
            try
            {
                for (ulong id = 10; id < 30; id++)
                {
                    shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(id, 1f, 0f, 0f, 0f)]));
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        });
        try
        {
            for (int i = 0; i < 10; i++)
            {
                shard.Flush();
            }
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }

        await writer;
        Assert.All(errors, error => Assert.IsType<ShardClosedException>(error));
        Assert.True(shard.Count(new CountRequest()) >= 3ul);
    }

    [Fact]
    public async Task ParallelMultiShard_BothShardsReturnTopHit()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard first = SeedThree(out _);
        using EdgeShard second = SeedThree(out _);
        QueryRequest request = NearestQuery(1f, 0f, 0f, 0f, 1);
        Task<IReadOnlyList<ScoredPoint>>[] tasks =
        [
            Task.Run(() => first.Query(request)),
            Task.Run(() => second.Query(request)),
        ];
        IReadOnlyList<ScoredPoint>[] results = await Task.WhenAll(tasks);
        foreach (IReadOnlyList<ScoredPoint> hits in results)
        {
            ScoredPoint top = Assert.Single(hits);
            Assert.Equal(PointId.FromNumber(1), top.Id);
        }
    }

    // §19 unwritable path (POSIX only; chmod is ineffective for root/Windows).

    [Fact]
    public void Create_UnwritablePath_ThrowsOperationError()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (Environment.UserName == "root")
        {
            return;
        }

        string target = Path.Combine(TestHelpers.TempDir(), "shard");
        Directory.CreateDirectory(target);
        File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        try
        {
            Assert.Throws<EdgeOperationException>(() => EdgeShard.Create(Path.Combine(target, "inner"), TestHelpers.FourDimDotConfig()));
        }
        finally
        {
            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    // §19 bad-key corpus at the engine boundary.

    [Fact]
    public void BadUuidKey_RejectedAndShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => PointId.FromString("not-a-uuid"));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void BadJsonPathKey_RejectedAndShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        CountRequest request = new()
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "a[", Match = Match.CreateValue(ValueVariants.String("x")) })],
            },
        };
        Assert.ThrowsAny<EdgeException>(() => shard.Count(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Lat999_RejectedAndShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        CountRequest request = new()
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoRadius = new GeoRadius { Center = new GeoPoint { Lon = 0, Lat = 999 }, Radius = 1000 },
                })],
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Count(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void UnclosedPolygon_RejectedAndShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        GeoLineString ring = new()
        {
            Points =
            [
                new GeoPoint { Lon = 0, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 0 },
                new GeoPoint { Lon = 1, Lat = 1 },
                new GeoPoint { Lon = 0, Lat = 1 },
            ],
        };
        CountRequest request = new()
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "loc", GeoPolygon = new GeoPolygon { Exterior = ring } })],
            },
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Count(request));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void OversizedHnsw_RejectedAndShardUsable()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.SetHnswConfig(new HnswIndexConfig { M = 5000 }));
        Assert.Equal(3ul, shard.Count(new CountRequest()));
    }

    // §19 order-by desc + datetime start-from.

    private static EdgeShard RankShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("rank", PayloadSchemaType.Integer));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 3 } },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["rank"] = 1 } },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["rank"] = 2 } },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }

    [Fact]
    public void Query_OrderByDesc_ReturnsDescendingRank()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = RankShard(out _);
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 10,
            Query = ScoringQuery.CreateOrderBy(new OrderBy { Key = "rank", Direction = Direction.Desc }),
        });
        Assert.Equal(
            [PointId.FromNumber(1), PointId.FromNumber(3), PointId.FromNumber(2)],
            hits.Select(hit => hit.Id).ToList());
    }

    [Fact]
    public void Scroll_OrderByStartFromDatetime_ResumesAfterStart()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = SeedThree(out _);
        shard.Update(UpdateOperation.CreateFieldIndex("ts", PayloadSchemaType.Datetime));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(4, 1f, 0f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-01-10T00:00:00Z" } },
            TestHelpers.FourDimPoint(5, 0f, 1f, 0f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-02-10T00:00:00Z" } },
            TestHelpers.FourDimPoint(6, 0f, 0f, 1f, 0f) with { Payload = new JsonObject { ["ts"] = "2024-03-10T00:00:00Z" } },
        ]));
        ScrollResponse page = shard.Scroll(new ScrollRequest
        {
            Limit = 10,
            OrderBy = new OrderBy
            {
                Key = "ts",
                Direction = Direction.Asc,
                StartFrom = new DatetimeStartFrom("2024-02-01T00:00:00Z"),
            },
        });
        Assert.Equal([PointId.FromNumber(5), PointId.FromNumber(6)], page.Records.Select(record => record.Id).ToList());
    }

    // §19 geo filters.

    private static EdgeShard GeoShard(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndex("loc", PayloadSchemaType.Geo));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["loc"] = new JsonObject { ["lon"] = 13.4, ["lat"] = 52.5 } },
                },
                TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with
                {
                    Payload = new JsonObject { ["loc"] = new JsonObject { ["lon"] = 2.35, ["lat"] = 48.85 } },
                },
                TestHelpers.FourDimPoint(3, 0f, 0f, 1f, 0f) with
                {
                    Payload = new JsonObject { ["loc"] = new JsonObject { ["lon"] = -74.0, ["lat"] = 40.7 } },
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

    [Fact]
    public void GeoBoundingBox_CountsEuropeanPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoBoundingBox = new GeoBoundingBox
                    {
                        TopLeft = new GeoPoint { Lon = -10, Lat = 60 },
                        BottomRight = new GeoPoint { Lon = 30, Lat = 40 },
                    },
                })],
            },
        }));
    }

    [Fact]
    public void GeoRadius_CountsNearbyPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition
                {
                    Key = "loc",
                    GeoRadius = new GeoRadius { Center = new GeoPoint { Lon = 13.4, Lat = 52.5 }, Radius = 500_000 },
                })],
            },
        }));
    }

    [Fact]
    public void GeoPolygon_CountsEnclosedPoints()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = GeoShard(out _);
        GeoLineString ring = new()
        {
            Points =
            [
                new GeoPoint { Lon = -10, Lat = 40 },
                new GeoPoint { Lon = 30, Lat = 40 },
                new GeoPoint { Lon = 30, Lat = 60 },
                new GeoPoint { Lon = -10, Lat = 60 },
                new GeoPoint { Lon = -10, Lat = 40 },
            ],
        };
        Assert.Equal(2ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "loc", GeoPolygon = new GeoPolygon { Exterior = ring } })],
            },
        }));
    }
}
