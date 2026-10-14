using System.Reflection;
using System.Text.Json;
using Xunit.Abstractions;

namespace Qdrant.Edge.Tests;

/// <summary>Remaining test.md gaps: config-merge derivation/defaults (§19),
/// WithParams index per type (§14), OrderBy engine rejection + ThrowForStatus
/// mapping (§15). No-op when the native engine is absent, except the
/// ThrowForStatus reflection pins which need no engine.</summary>
public sealed class RemainingGapsTests
{
    private readonly ITestOutputHelper _output;

    public RemainingGapsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ConfigMerge_MissingFileDerivation_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        EdgeShard created = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            created.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
            created.Flush();
            created.Unload();
        }
        finally
        {
            created.Dispose();
        }

        string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        _output.WriteLine("Shard files: " + string.Join(", ", files.Select(f => Path.GetRelativePath(dir, f))));
        string? configFile = files.FirstOrDefault(
            f => Path.GetFileName(f).Contains("config", StringComparison.OrdinalIgnoreCase));
        if (configFile is not null)
        {
            File.Move(configFile, configFile + ".bak");
            _output.WriteLine("Renamed config file: " + Path.GetRelativePath(dir, configFile));
        }
        else
        {
            _output.WriteLine("No config file on disk; config is derived from segments on load.");
        }

        using EdgeShard reloaded = EdgeShard.Load(dir);
        Assert.Equal(1ul, reloaded.Count(new CountRequest()));
        IReadOnlyList<ScoredPoint> hits = reloaded.Query(new QueryRequest
        {
            Limit = 1,
            Query = ScoringQuery.CreateVector(Query.Nearest(NamedVector.Dense([1f, 2f, 3f, 4f]))),
        });
        ScoredPoint hit = Assert.Single(hits);
        Assert.Equal(PointId.FromNumber(1), hit.Id);
    }

    [Fact]
    public void ConfigMerge_NewShardDefaults_Native()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        EdgeConfig echoed = shard.GetConfig();
        _output.WriteLine("Effective config: " + JsonSerializer.Serialize(echoed, EdgeJsonContext.Deep.EdgeConfig));
        Assert.True(echoed.VectorData.ContainsKey(string.Empty));
        Assert.Equal(4ul, echoed.VectorData[string.Empty].Size);
        Assert.Equal(Distance.Dot, echoed.VectorData[string.Empty].Distance);
    }

    public static IEnumerable<object?[]> WithParamsCases() =>
    [
        ["f_keyword", PayloadSchemaType.Keyword, PayloadIndexParams.Keyword(new KeywordIndexParams())],
        ["f_integer", PayloadSchemaType.Integer, PayloadIndexParams.Integer(new IntegerIndexParams())],
        ["f_float", PayloadSchemaType.Float, PayloadIndexParams.Float(new FloatIndexParams())],
        ["f_geo", PayloadSchemaType.Geo, PayloadIndexParams.Geo(new GeoIndexParams())],
        ["f_text", PayloadSchemaType.Text, PayloadIndexParams.Text(new TextIndexParams())],
        ["f_bool", PayloadSchemaType.Bool, PayloadIndexParams.Bool(new BoolIndexParams())],
        ["f_datetime", PayloadSchemaType.Datetime, PayloadIndexParams.Datetime(new DatetimeIndexParams())],
        ["f_uuid", PayloadSchemaType.Uuid, PayloadIndexParams.Uuid(new UuidIndexParams())],
    ];

    [Theory]
    [MemberData(nameof(WithParamsCases))]
    public void CreateFieldIndexWithParams_EachType_Native(string field, PayloadSchemaType expected, PayloadIndexParams indexParams)
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.CreateFieldIndexWithParams(field, indexParams));
        ShardInfo info = shard.GetInfo();
        Assert.True(info.PayloadSchema.TryGetValue(field, out PayloadIndexInfo? entry));
        Assert.Equal(expected, entry.DataType);
    }

    [Fact]
    public void Scroll_InvalidOrderByKey_NativeRejectedWithReason()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
        EdgeInvalidArgumentException thrown = Assert.Throws<EdgeInvalidArgumentException>(() =>
            shard.Scroll(new ScrollRequest { Limit = 10, OrderBy = new OrderBy { Key = "!!!" } }));
        Assert.False(string.IsNullOrEmpty(thrown.Reason));
        Assert.Equal(thrown.Reason, thrown.Message);
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Query_OrderByInvalidKey_NativeRejectedWithReason()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
        EdgeInvalidArgumentException thrown = Assert.Throws<EdgeInvalidArgumentException>(() =>
            shard.Query(new QueryRequest
            {
                Limit = 10,
                Query = ScoringQuery.CreateOrderBy(new OrderBy { Key = "!!!" }),
            }));
        Assert.False(string.IsNullOrEmpty(thrown.Reason));
        Assert.Equal(thrown.Reason, thrown.Message);
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void Scroll_EmptyOrderByKey_NativeRejectedWithoutAborting()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        string dir = TestHelpers.TempDir();
        using EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        shard.Update(UpdateOperation.UpsertPoints([TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f)]));
        Assert.Throws<EdgeInvalidArgumentException>(() =>
            shard.Scroll(new ScrollRequest { Limit = 10, OrderBy = new OrderBy { Key = string.Empty } }));
        Assert.Equal(1ul, shard.Count(new CountRequest()));
    }

    [Fact]
    public void ThrowForStatus_NativeShardClosed_MapsToShardClosedException()
    {
        Exception caught = InvokeThrowForStatus(QdrantEdgeStatus.ShardClosed, "probe");
        Assert.IsType<ShardClosedException>(caught);
    }

    [Fact]
    public void ThrowForStatus_UnknownStatus_MapsToOperationError()
    {
        Exception caught = InvokeThrowForStatus((QdrantEdgeStatus)99, "probe");
        Assert.IsType<EdgeOperationException>(caught);
    }

    private static Exception InvokeThrowForStatus(QdrantEdgeStatus status, string method)
    {
        MethodInfo? target = typeof(EdgeShard).GetMethod("ThrowForStatus", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(target);
        try
        {
            target.Invoke(null, [status, method]);
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException is Exception inner)
            {
                return inner;
            }

            throw;
        }

        Assert.Fail("ThrowForStatus did not throw.");
        throw new InvalidOperationException("unreachable");
    }
}
