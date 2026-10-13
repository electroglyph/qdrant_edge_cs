using System.Text.Json;

namespace Qdrant.Edge.Tests;

/// <summary>Sparse and multi-dense round-trip parity pins; no-op when native is absent.</summary>
public sealed class EdgeShardSparseMultiNativeTests
{
    [Fact]
    public void SparseVector_RoundTripsThroughRetrieve()
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
                    ["text"] = NamedVector.Sparse(new SparseVector
                    {
                        Indices = [1, 5, 9],
                        Values = [0.5f, 1.5f, 2.5f],
                    }),
                }),
            },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(true),
        });
        Record only = Assert.Single(records);
        Assert.NotNull(only.Vector);
        Vector? roundTripped = JsonSerializer.Deserialize<Vector>(only.Vector);
        NamedVectorMap map = Assert.IsType<NamedVectorMap>(roundTripped);
        SparseNamedVector sparse = Assert.IsType<SparseNamedVector>(map.Map["text"]);
        Assert.Equal([1u, 5u, 9u], sparse.Vector.Indices);
        Assert.Equal([0.5f, 1.5f, 2.5f], sparse.Vector.Values);
    }

    [Fact]
    public void MultiDenseVector_RoundTripsThroughRetrieve()
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
                [string.Empty] = new VectorDataConfig
                {
                    Size = 2,
                    Distance = Distance.Dot,
                    MultivectorConfig = new MultiVectorConfig
                    {
                        Comparator = MultiVectorComparator.MaxSim,
                    },
                },
            },
        };
        using EdgeShard shard = EdgeShard.Create(dir, config);
        shard.Update(UpdateOperation.UpsertPoints(
        [
            new Point
            {
                Id = PointId.FromNumber(1),
                Vector = Vector.MultiDense([[1f, 2f], [3f, 4f], [5f, 6f]]),
            },
        ]));
        IReadOnlyList<Record> records = shard.Retrieve(new RetrieveRequest
        {
            PointIds = [PointId.FromNumber(1)],
            WithVector = WithVector.Bool(true),
        });
        Record only = Assert.Single(records);
        Assert.NotNull(only.Vector);
        Vector? roundTripped = JsonSerializer.Deserialize<Vector>(only.Vector);
        MultiDenseVector multi = Assert.IsType<MultiDenseVector>(roundTripped);
        Assert.Equal([1f, 2f, 3f, 4f, 5f, 6f], multi.Vectors.SelectMany(row => row));
    }
}
