using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>v1.20.0 edge coverage: WAL-less mode, nested indexes, BM25 scoring, code tokenizer, Turbo types, text queries.</summary>
public sealed class EdgeShardV120Tests
{
    [Fact]
    public void WalEnabled_Null_PassesValidation()
    {
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = null };
        config.Validate();
    }

    [Fact]
    public void WalEnabled_False_PassesValidation()
    {
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        config.Validate();
    }

    [Fact]
    public void WalEnabled_True_PassesValidation()
    {
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = true };
        config.Validate();
    }

    [Fact]
    public void WalEnabled_False_RoundTripsThroughJson()
    {
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        string json = JsonSerializer.Serialize(config, EdgeJsonContext.Deep.EdgeConfig);
        Assert.Contains("WalEnabled", json);
        EdgeConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.EdgeConfig);
        Assert.NotNull(back);
        Assert.False(back.WalEnabled);
    }

    [Fact]
    public void Nested_Keyword_PassesValidation()
    {
        KeywordIndexParams index = new() { Nested = "offers" };
        index.Validate();
    }

    [Fact]
    public void Nested_Empty_ThrowsArgument()
    {
        KeywordIndexParams index = new() { Nested = "" };
        Assert.Throws<ArgumentException>(index.Validate);
    }

    [Fact]
    public void Nested_Whitespace_ThrowsArgument()
    {
        KeywordIndexParams index = new() { Nested = "  " };
        Assert.Throws<ArgumentException>(index.Validate);
    }

    [Fact]
    public void Nested_Integer_InvalidPath_Propagates()
    {
        IntegerIndexParams index = new() { Lookup = true, Nested = "" };
        Assert.Throws<ArgumentException>(index.Validate);
    }

    [Fact]
    public void TextIndex_ScoringBm25_PassesValidation()
    {
        TextIndexParams index = new() { Scoring = new TextScoringParams { Type = TextScoringType.Bm25 } };
        index.Validate();
    }

    [Fact]
    public void TextIndex_InvertedLength_ThrowsInvalidArgument()
    {
        TextIndexParams index = new() { MinTokenLen = 5, MaxTokenLen = 3 };
        Assert.Throws<EdgeInvalidArgumentException>(index.Validate);
    }

    [Fact]
    public void Tokenizer_Code_RoundTripsThroughJson()
    {
        TextIndexParams index = new() { Tokenizer = TokenizerType.Code };
        PayloadIndexParams wrapped = PayloadIndexParams.Text(index);
        string json = JsonSerializer.Serialize(wrapped, EdgeJsonContext.Deep.PayloadIndexParams);
        PayloadIndexParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexParams);
        TextPayloadIndex text = Assert.IsType<TextPayloadIndex>(back);
        Assert.Equal(TokenizerType.Code, text.Config.Tokenizer);
    }

    [Fact]
    public void Turbo_Datatypes_RoundTripThroughJson()
    {
        foreach (VectorStorageDatatype datatype in new[] { VectorStorageDatatype.Turbo8, VectorStorageDatatype.Turbo16 })
        {
            VectorDataConfig field = new() { Size = 4, Distance = Distance.Dot, Datatype = datatype };
            string json = JsonSerializer.Serialize(field, EdgeJsonContext.Deep.VectorDataConfig);
            VectorDataConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VectorDataConfig);
            Assert.NotNull(back);
            Assert.Equal(datatype, back.Datatype);
        }
    }

    [Fact]
    public void Turbo_Bits8_RoundTripsThroughJson()
    {
        TurboQuantizationParams @params = new() { Bits = TurboQuantBitSize.Bits8 };
        QuantizationConfig config = QuantizationConfig.Turbo(@params);
        string json = JsonSerializer.Serialize(config, EdgeJsonContext.Deep.QuantizationConfig);
        QuantizationConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.QuantizationConfig);
        TurboQuantization turbo = Assert.IsType<TurboQuantization>(back);
        Assert.Equal(TurboQuantBitSize.Bits8, turbo.Config.Bits);
    }

    [Fact]
    public void Bm25Params_Defaults_PassValidation()
    {
        new Bm25Params { K = null, B = null }.Validate();
    }

    [Fact]
    public void Bm25Params_NegativeK_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { K = -0.5f }.Validate());
    }

    [Fact]
    public void Bm25Params_NaNK_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { K = float.NaN }.Validate());
    }

    [Fact]
    public void Bm25Params_BAboveOne_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { B = 1.5f }.Validate());
    }

    [Fact]
    public void Bm25Params_BelowZero_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { B = -0.1f }.Validate());
    }

    [Fact]
    public void TextQuery_EmptyField_ThrowsArgument()
    {
        ScoringQuery query = ScoringQuery.CreateText("", "hello");
        Assert.Throws<ArgumentException>(query.Validate);
    }

    [Fact]
    public void TextQuery_EmptyQuery_ThrowsArgument()
    {
        ScoringQuery query = ScoringQuery.CreateText("title", "  ");
        Assert.Throws<ArgumentException>(query.Validate);
    }

    [Fact]
    public void TextQuery_InvalidScoring_Propagates()
    {
        ScoringQuery query = ScoringQuery.CreateText("title", "hello", TextQueryScoring.Bm25(new Bm25Params { K = -1f }));
        Assert.Throws<EdgeInvalidArgumentException>(query.Validate);
    }

    [Fact]
    public void TextQuery_Valid_PassesValidation()
    {
        ScoringQuery.CreateText("title", "hello").Validate();
        ScoringQuery.CreateText("title", "hello", TextQueryScoring.Bm25(new Bm25Params { K = 1.2f, B = 0.75f })).Validate();
    }

    [Fact]
    public void TextScoring_Bm25_RoundTripsThroughJson()
    {
        ScoringQuery query = ScoringQuery.CreateText("title", "hello", TextQueryScoring.Bm25(new Bm25Params { K = 1.2f }));
        string json = JsonSerializer.Serialize(query, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
        TextScoringQuery text = Assert.IsType<TextScoringQuery>(back);
        Assert.Equal("title", text.Field);
        Assert.Equal("hello", text.Query);
    }

    [Fact]
    public void WalEnabled_False_CreatesShardNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void WalEnabled_False_PersistsAfterFlushAndReload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        using (EdgeShard shard = EdgeShard.Create(dir, config))
        {
            shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
            shard.Flush();
            Assert.Equal(1ul, shard.Count(new CountRequest { Exact = true }));
        }

        using EdgeShard reopened = EdgeShard.Load(dir);
        Assert.Equal(1ul, reopened.Count(new CountRequest { Exact = true }));
    }

    [Fact]
    public void WalEnabled_MismatchOnLoad_FailsNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using (EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig()))
        {
            shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
            shard.Flush();
        }

        EdgeConfig mismatched = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        EdgeOperationException ex = Assert.Throws<EdgeOperationException>(() => EdgeShard.Load(dir, mismatched));
        Assert.Contains("wal_enabled", ex.Reason);
    }

    [Fact]
    public void Flush_AfterUpserts_PreservesPointsOnReload()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using (EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig()))
        {
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f),
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f),
            ]));
            shard.Flush();
        }

        using EdgeShard reopened = EdgeShard.Load(dir);
        Assert.Equal(2ul, reopened.Count(new CountRequest { Exact = true }));
    }

    [Fact]
    public void Nested_KeywordIndex_CreatesNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "offers[].price",
            PayloadIndexParams.Keyword(new KeywordIndexParams { Nested = "offers" })));
        ShardInfo info = shard.GetInfo();
        Assert.True(info.PayloadSchema.ContainsKey("offers[].price"));
    }

    [Fact]
    public void Nested_InvalidPath_RejectedNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        UpdateOperation op = UpdateOperation.CreateFieldIndexWithParams(
            "color",
            PayloadIndexParams.Keyword(new KeywordIndexParams { Nested = "!!!" }));
        Assert.Throws<EdgeInvalidArgumentException>(() => shard.Update(op));
        Assert.True(shard.IsLoaded);
    }

    [Fact]
    public void TextScoring_Bm25_CreatesIndexNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Scoring = new TextScoringParams { Type = TextScoringType.Bm25 },
            })));
        ShardInfo info = shard.GetInfo();
        Assert.True(info.PayloadSchema.ContainsKey("title"));
    }

    [Fact]
    public void CodeTokenizer_CreatesIndexNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams { Tokenizer = TokenizerType.Code })));
        ShardInfo info = shard.GetInfo();
        Assert.True(info.PayloadSchema.ContainsKey("title"));
    }

    [Fact]
    public void TextQuery_Bm25_RanksNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Scoring = new TextScoringParams { Type = TextScoringType.Bm25 },
            })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                Payload = new JsonObject { ["title"] = "the quick brown fox" },
            },
            TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
            {
                Payload = new JsonObject { ["title"] = "unrelated document about cooking" },
            },
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 2,
            Query = ScoringQuery.CreateText("title", "quick fox"),
        });
        Assert.Single(hits);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void TextQuery_EmptyField_RejectedNativeWithoutAborting()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        QueryRequest request = new()
        {
            Limit = 2,
            Query = ScoringQuery.CreateText("", "hello"),
        };
        Assert.Throws<ArgumentException>(() => shard.Query(request));
        Assert.Equal(0ul, shard.Count(new CountRequest { Exact = true }));
    }

    [Fact]
    public void Turbo8_Datatype_EchoesViaGetConfig()
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
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot, Datatype = VectorStorageDatatype.Turbo8 },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        Assert.Equal(VectorStorageDatatype.Turbo8, shard.GetConfig().VectorData[""].Datatype);
    }

    [Fact]
    public void Turbo16_Datatype_EchoesViaGetConfig()
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
                [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot, Datatype = VectorStorageDatatype.Turbo16 },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        Assert.Equal(VectorStorageDatatype.Turbo16, shard.GetConfig().VectorData[""].Datatype);
    }

    [Fact]
    public void Turbo_Bits8_EchoesViaGetConfig()
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
                [""] = new VectorDataConfig
                {
                    Size = 4,
                    Distance = Distance.Dot,
                    QuantizationConfig = QuantizationConfig.Turbo(new TurboQuantizationParams { Bits = TurboQuantBitSize.Bits8 }),
                },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        TurboQuantization turbo = Assert.IsType<TurboQuantization>(shard.GetConfig().VectorData[""].QuantizationConfig);
        Assert.Equal(TurboQuantBitSize.Bits8, turbo.Config.Bits);
    }

    [Fact]
    public void Sparse_Turbo8_EchoesViaGetConfig()
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
                ["splade"] = new SparseVectorDataConfig { Modifier = Modifier.Idf, Datatype = VectorStorageDatatype.Turbo8 },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        Assert.Equal(VectorStorageDatatype.Turbo8, shard.GetConfig().SparseVectorData["splade"].Datatype);
    }

    [Fact]
    public void CreateDenseVector_Turbo16_EchoesViaGetConfig()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateDenseVector("img", 4, Distance.Dot, null, VectorStorageDatatype.Turbo16));
        VectorDataConfig field = shard.GetConfig().VectorData["img"];
        Assert.Equal(4ul, field.Size);
        Assert.Equal(VectorStorageDatatype.Turbo16, field.Datatype);
    }

    [Fact]
    public void WalEnabled_False_EchoesViaGetConfig()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeConfig config = TestHelpers.FourDimDotConfig() with { WalEnabled = false };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        Assert.Equal(false, shard.GetConfig().WalEnabled);
    }

    [Fact]
    public void WalEnabled_Unset_ReturnsNullViaGetConfig()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        Assert.Null(shard.GetConfig().WalEnabled);
    }

    [Fact]
    public void TextQuery_ExplicitBm25_RanksNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Scoring = new TextScoringParams { Type = TextScoringType.Bm25 },
            })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                Payload = new JsonObject { ["title"] = "the quick brown fox" },
            },
            TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
            {
                Payload = new JsonObject { ["title"] = "unrelated document about cooking" },
            },
        ]));
        IReadOnlyList<ScoredPoint> hits = shard.Query(new QueryRequest
        {
            Limit = 2,
            Query = ScoringQuery.CreateText("title", "quick fox", TextQueryScoring.Bm25(new Bm25Params { K = 1.2f, B = 0.75f })),
        });
        Assert.Single(hits);
        Assert.Equal(PointId.FromNumber(1), hits[0].Id);
    }

    [Fact]
    public void TextScoring_ExplicitParams_RoundTripThroughJson()
    {
        ScoringQuery query = ScoringQuery.CreateText("title", "hello", TextQueryScoring.Bm25(new Bm25Params { K = 1.2f, B = 0.75f }));
        string json = JsonSerializer.Serialize(query, EdgeJsonContext.Deep.ScoringQuery);
        ScoringQuery? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScoringQuery);
        TextScoringQuery text = Assert.IsType<TextScoringQuery>(back);
        Bm25TextScoring scoring = Assert.IsType<Bm25TextScoring>(text.Scoring);
        Assert.Equal(1.2f, scoring.Params.K);
        Assert.Equal(0.75f, scoring.Params.B);
    }

    [Fact]
    public void Bm25Params_PositiveInfinityK_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { K = float.PositiveInfinity }.Validate());
    }

    [Fact]
    public void Bm25Params_NaNB_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { B = float.NaN }.Validate());
    }

    [Fact]
    public void Bm25Params_PositiveInfinityB_ThrowsInvalidArgument()
    {
        Assert.Throws<EdgeInvalidArgumentException>(() => new Bm25Params { B = float.PositiveInfinity }.Validate());
    }

    [Fact]
    public void Query_OnNonscoringIndex_ThrowsOperationWithoutAborting()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "title",
            PayloadIndexParams.Text(new TextIndexParams { })));
        shard.Update(UpdateOperation.UpsertPoints(
        [
            TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
            {
                Payload = new JsonObject { ["title"] = "hello world" },
            },
        ]));
        EdgeOperationException ex = Assert.Throws<EdgeOperationException>(() => shard.Query(new QueryRequest
        {
            Limit = 2,
            Query = ScoringQuery.CreateText("title", "hello"),
        }));
        Assert.Contains("does not score", ex.Reason);
        Assert.True(shard.IsLoaded);
        Assert.Equal(1ul, shard.Count(new CountRequest { Exact = true }));
    }

    [Fact]
    public void WrongNested_SemanticMismatch_ThrowsOperationWithoutAborting()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        UpdateOperation op = UpdateOperation.CreateFieldIndexWithParams(
            "color",
            PayloadIndexParams.Keyword(new KeywordIndexParams { Nested = "offers" }));
        EdgeOperationException ex = Assert.Throws<EdgeOperationException>(() => shard.Update(op));
        Assert.Contains("must be inside the elements", ex.Reason);
        Assert.True(shard.IsLoaded);
        Assert.Equal(0ul, shard.Count(new CountRequest { Exact = true }));
    }

    [Fact]
    public void CodeScoringNested_Combined_CreatesNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "docs[].title",
            PayloadIndexParams.Text(new TextIndexParams
            {
                Tokenizer = TokenizerType.Code,
                Scoring = new TextScoringParams { Type = TextScoringType.Bm25 },
                Nested = "docs",
            })));
        Assert.True(shard.GetInfo().PayloadSchema.ContainsKey("docs[].title"));
    }

    [Fact]
    public void Nested_Float_CreatesNative()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(
            "offers[].price",
            PayloadIndexParams.Float(new FloatIndexParams { Nested = "offers" })));
        Assert.True(shard.GetInfo().PayloadSchema.ContainsKey("offers[].price"));
    }
}
