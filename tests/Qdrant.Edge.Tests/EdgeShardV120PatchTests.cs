using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>v1.20.1/v1.20.2 engine regression pins: behavior upstream fixed after v1.20.0.</summary>
public sealed class EdgeShardV120PatchTests
{
    [Fact]
    public void Recommend_Euclid_BestScore_ThresholdKeepsNearerPoint()
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
            TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f),
            TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f),
            TestHelpers.FourDimPoint(3, -1f, 0f, 0f, 0f),
        ]));
        QueryRequest unfiltered = new()
        {
            Limit = 10,
            Query = ScoringQuery.CreateVector(Query.Recommend(
                [NamedVector.Dense([1f, 0f, 0f, 0f])], [], RecommendStrategy.BestScore)),
        };
        IReadOnlyList<ScoredPoint> ranked = shard.Query(unfiltered);
        Assert.True(ranked.Count >= 2, "expected at least two recommend hits to place a threshold between");
        float threshold = (ranked[0].Score + ranked[1].Score) / 2f;
        IReadOnlyList<ScoredPoint> hits = shard.Query(unfiltered with { ScoreThreshold = threshold });
        ScoredPoint single = Assert.Single(hits);
        Assert.Equal(ranked[0].Id, single.Id);
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void QueryGroups_ObjectValuedKeys_StillReturnsValidGroups()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        List<Point> points = [];
        for (ulong id = 1; id <= 6; id++)
        {
            points.Add(TestHelpers.FourDimPoint(id, 11f - id, 0f, 0f, 0f) with
            {
                Payload = new JsonObject { ["g"] = new JsonObject { ["nested"] = "object" } },
            });
        }

        points.Add(TestHelpers.FourDimPoint(101, 2f, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["g"] = "valid-a" },
        });
        points.Add(TestHelpers.FourDimPoint(102, 1f, 0f, 0f, 0f) with
        {
            Payload = new JsonObject { ["g"] = "valid-b" },
        });
        shard.Update(UpdateOperation.UpsertPoints(points));
        IReadOnlyList<Group> groups = shard.QueryGroups(new GroupRequest
        {
            Query = new QueryRequest
            {
                Limit = 2,
                Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))),
            },
            GroupBy = "g",
            Groups = 2,
            GroupSize = 1,
        });
        Assert.Equal(2, groups.Count);
        Assert.Equal(["valid-a", "valid-b"], groups.Select(group => ((StringGroupId)group.Key).Value).Order());
        Assert.All(groups, group => Assert.Single(group.Hits));
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void TextIndex_GreekStemmer_MatchFilterCountsNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Tokenizer = TokenizerType.Word,
                MinTokenLen = 2,
                MaxTokenLen = 20,
                Lowercase = true,
                Stemmer = Stemmer.Snowball(SnowballLanguage.Greek),
            })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                // Stems to "αντιθετ"; the pre-1.2.3 stemmer panicked on this word.
                Payload = new JsonObject { ["title"] = "αντίθετε" },
            },
            TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
            {
                Payload = new JsonObject { ["title"] = "μαγειρεύω νόστιμο φαγητό" },
            },
        ]));
        Assert.Equal(1ul, shard.Count(new CountRequest
        {
            Filter = new Filter
            {
                Must = [Condition.Field(new FieldCondition { Key = "title", Match = Match.CreateText("αντίθετε") })],
            },
        }));
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void TextIndex_GreekStemmer_EchoesViaGetInfo()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Tokenizer = TokenizerType.Word,
                Stemmer = Stemmer.Snowball(SnowballLanguage.Greek),
            })));
        TextPayloadIndex index = Assert.IsType<TextPayloadIndex>(shard.GetInfo().PayloadSchema["title"].Params);
        Assert.Equal(TokenizerType.Word, index.Config.Tokenizer);
        SnowballStemmer stemmer = Assert.IsType<SnowballStemmer>(index.Config.Stemmer);
        Assert.Equal(SnowballLanguage.Greek, stemmer.Language);
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void FormulaGeoDistance_MultiGeo_UsesNearestPoint()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = EdgeShard.Create(TestHelpers.TempDir(), TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 0f, 0f, 0f) with
            {
                Payload = new JsonObject
                {
                    ["location"] = new JsonArray
                    {
                        new JsonObject { ["lon"] = 10.0, ["lat"] = 10.0 },
                        new JsonObject { ["lon"] = 1.0, ["lat"] = 1.0 },
                    },
                },
            },
            TestHelpers.FourDimPoint(2, 0f, 1f, 0f, 0f) with
            {
                Payload = new JsonObject
                {
                    ["location"] = new JsonObject { ["lon"] = 1.0, ["lat"] = 1.0 },
                },
            },
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 2,
            Prefetches =
            [
                new Prefetch { Limit = 2, Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 0f, 0f, 0f]))) },
            ],
            Query = ScoringQuery.Formula(Expression.GeoDistance(new GeoPoint { Lon = 0, Lat = 0 }, "location")),
        });
        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.True(float.IsFinite(hit.Score)));
        Assert.Equal(hits[0].Score, hits[1].Score, 3);
        Assert.True(shard.IsLoaded);
    }
}
