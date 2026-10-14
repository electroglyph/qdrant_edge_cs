using System.Text.Json;

namespace Qdrant.Edge.Tests;

public sealed class VectorGapsTests
{
    private sealed record FakeVector : Vector;

    private sealed record FakeNamedVector : NamedVector;

    private sealed class NullKeyMap(NamedVector value) : IReadOnlyDictionary<string, NamedVector>
    {
        public NamedVector this[string key] => value;

        public IEnumerable<string> Keys => [null!];

        public IEnumerable<NamedVector> Values => [value];

        public int Count => 1;

        public bool ContainsKey(string key) => key is null;

        public bool TryGetValue(string key, out NamedVector result)
        {
            result = value;
            return key is null;
        }

        public IEnumerator<KeyValuePair<string, NamedVector>> GetEnumerator()
        {
            yield return new KeyValuePair<string, NamedVector>(null!, value);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void VectorSingle_NullValues_ThrowsArgumentNull()
    {
        Vector vector = Vector.Single(null!);
        Assert.Throws<ArgumentNullException>(vector.Validate);
    }

    [Fact]
    public void VectorSingle_Oversized_ThrowsInvalidArgument()
    {
        Vector vector = Vector.Single(Enumerable.Repeat(1f, 65_537).ToList());
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorSingle_PosInf_Throws()
    {
        Vector vector = Vector.Single([1f, float.PositiveInfinity]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorSingle_NegInf_Throws()
    {
        Vector vector = Vector.Single([1f, float.NegativeInfinity]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorSingle_AtCap_Passes()
    {
        Vector vector = Vector.Single(Enumerable.Repeat(1f, 65_536).ToList());
        vector.Validate();
    }

    [Fact]
    public void VectorMultiDense_NullRows_ThrowsArgumentNull()
    {
        Vector vector = Vector.MultiDense(null!);
        Assert.Throws<ArgumentNullException>(vector.Validate);
    }

    [Fact]
    public void VectorMultiDense_NullRow_ThrowsArgumentNull()
    {
        Vector vector = Vector.MultiDense([null!]);
        Assert.Throws<ArgumentNullException>(vector.Validate);
    }

    [Fact]
    public void VectorMultiDense_EmptyRow_Throws()
    {
        Vector vector = Vector.MultiDense([Array.Empty<float>()]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorMultiDense_OversizedRow_Throws()
    {
        Vector vector = Vector.MultiDense([Enumerable.Repeat(1f, 65_537).ToList()]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void VectorMultiDense_NonFiniteElement_Throws(float bad)
    {
        Vector vector = Vector.MultiDense([[1f, bad]]);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorMultiDense_SingleRow_Passes()
    {
        Vector vector = Vector.MultiDense([[1f, 2f]]);
        vector.Validate();
    }

    [Fact]
    public void VectorNamed_NullMap_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => Vector.Named(null!));
    }

    [Fact]
    public void VectorNamed_NullName_ThrowsArgumentNull()
    {
        var map = new NullKeyMap(NamedVector.Dense([1f]));
        Assert.Throws<ArgumentNullException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_NullValue_ThrowsArgumentNull()
    {
        var map = new Dictionary<string, NamedVector> { ["a"] = null! };
        Assert.Throws<ArgumentNullException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_EmptyMap_Passes()
    {
        Vector.Named(new Dictionary<string, NamedVector>());
    }

    [Fact]
    public void VectorNamed_EmptyStringKey_Passes()
    {
        var map = new Dictionary<string, NamedVector> { [""] = NamedVector.Dense([1f]) };
        Vector.Named(map);
    }

    [Fact]
    public void VectorNamed_DenseNull_Throws()
    {
        var map = new Dictionary<string, NamedVector> { ["a"] = new DenseNamedVector(null!) };
        Assert.Throws<ArgumentNullException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_DenseEmpty_Throws()
    {
        var map = new Dictionary<string, NamedVector> { ["a"] = NamedVector.Dense([]) };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_DenseOversized_Throws()
    {
        var map = new Dictionary<string, NamedVector>
        {
            ["a"] = NamedVector.Dense(Enumerable.Repeat(1f, 65_537).ToList()),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_DenseNonFinite_Throws()
    {
        var map = new Dictionary<string, NamedVector> { ["a"] = NamedVector.Dense([1f, float.NaN]) };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_SparseNull_Throws()
    {
        NamedVector named = NamedVector.Sparse(null!);
        Assert.Throws<ArgumentNullException>(named.Validate);
    }

    [Fact]
    public void VectorNamed_SparseNullViaVectorNamed_ThrowsNullReference()
    {
        var map = new Dictionary<string, NamedVector> { ["text"] = new SparseNamedVector(null!) };
        Assert.Throws<NullReferenceException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_SparseMismatched_Throws()
    {
        var map = new Dictionary<string, NamedVector>
        {
            ["text"] = NamedVector.Sparse(new SparseVector { Indices = [1u, 2u], Values = [1f] }),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_SparseNonFinite_Throws()
    {
        var map = new Dictionary<string, NamedVector>
        {
            ["text"] = NamedVector.Sparse(new SparseVector { Indices = [1u], Values = [float.NaN] }),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void VectorNamed_MultiDenseInvalid_Propagates()
    {
        var map = new Dictionary<string, NamedVector>
        {
            ["a"] = NamedVector.MultiDense([[1f, 2f], [1f, 2f, 3f]]),
        };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(map));
    }

    [Fact]
    public void Vector_UnknownShape_ThrowsInvalidArgument()
    {
        Vector forged = new FakeVector();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void VectorNamed_UnknownValueShape_ThrowsInvalidArgument()
    {
        var map = new Dictionary<string, NamedVector> { ["a"] = new FakeNamedVector() };
        var vector = new NamedVectorMap(map);
        Assert.Throws<EdgeInvalidArgumentException>(vector.Validate);
    }

    [Fact]
    public void VectorNamed_Factory_ValidatesEagerly()
    {
        var badMap = new Dictionary<string, NamedVector> { ["a"] = NamedVector.Dense([]) };
        Assert.Throws<EdgeInvalidArgumentException>(() => Vector.Named(badMap));
        var deferred = new NamedVectorMap(badMap);
        Assert.Throws<EdgeInvalidArgumentException>(deferred.Validate);
    }

    [Fact]
    public void Vector_Kinds_RoundTripThroughJson()
    {
        Vector single = Vector.Single([1f, 2f]);
        string singleJson = JsonSerializer.Serialize(single, EdgeJsonContext.Deep.Vector);
        SingleVector singleBack = Assert.IsType<SingleVector>(
            JsonSerializer.Deserialize(singleJson, EdgeJsonContext.Deep.Vector));
        Assert.Equal([1f, 2f], singleBack.Values);

        Vector multi = Vector.MultiDense([[1f, 2f], [3f, 4f]]);
        string multiJson = JsonSerializer.Serialize(multi, EdgeJsonContext.Deep.Vector);
        MultiDenseVector multiBack = Assert.IsType<MultiDenseVector>(
            JsonSerializer.Deserialize(multiJson, EdgeJsonContext.Deep.Vector));
        Assert.Equal([1f, 2f, 3f, 4f], multiBack.Vectors.SelectMany(row => row));

        Vector named = Vector.Named(new Dictionary<string, NamedVector>
        {
            [""] = NamedVector.Dense([1f, 2f]),
        });
        string namedJson = JsonSerializer.Serialize(named, EdgeJsonContext.Deep.Vector);
        NamedVectorMap namedBack = Assert.IsType<NamedVectorMap>(
            JsonSerializer.Deserialize(namedJson, EdgeJsonContext.Deep.Vector));
        DenseNamedVector denseBack = Assert.IsType<DenseNamedVector>(namedBack.Map[""]);
        Assert.Equal([1f, 2f], denseBack.Values);
    }

    [Fact]
    public void NamedVectorDense_Empty_Throws()
    {
        NamedVector named = NamedVector.Dense([]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorDense_Oversized_Throws()
    {
        NamedVector named = NamedVector.Dense(Enumerable.Repeat(1f, 65_537).ToList());
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorDense_NonFinite_Throws()
    {
        NamedVector named = NamedVector.Dense([1f, float.NaN]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorDense_Null_Throws()
    {
        NamedVector named = NamedVector.Dense(null!);
        Assert.Throws<ArgumentNullException>(named.Validate);
    }

    [Fact]
    public void NamedVectorSparse_Null_Throws()
    {
        var named = new SparseNamedVector(null!);
        Assert.Throws<ArgumentNullException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_NullRows_ThrowsArgumentNull()
    {
        NamedVector named = NamedVector.MultiDense(null!);
        Assert.Throws<ArgumentNullException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_Empty_Throws()
    {
        NamedVector named = NamedVector.MultiDense([]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_NullRow_Throws()
    {
        NamedVector named = NamedVector.MultiDense([null!]);
        Assert.Throws<ArgumentNullException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_EmptyRow_Throws()
    {
        NamedVector named = NamedVector.MultiDense([Array.Empty<float>()]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_Ragged_Throws()
    {
        NamedVector named = NamedVector.MultiDense([[1f, 2f], [1f, 2f, 3f]]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_OversizedRow_Throws()
    {
        NamedVector named = NamedVector.MultiDense([Enumerable.Repeat(1f, 65_537).ToList()]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVectorMultiDense_NonFinite_Throws()
    {
        NamedVector named = NamedVector.MultiDense([[1f, float.NaN]]);
        Assert.Throws<EdgeInvalidArgumentException>(named.Validate);
    }

    [Fact]
    public void NamedVector_UnknownShape_Throws()
    {
        NamedVector forged = new FakeNamedVector();
        Assert.Throws<EdgeInvalidArgumentException>(forged.Validate);
    }

    [Fact]
    public void SparseVector_NullIndices_Throws()
    {
        var sparse = new SparseVector { Indices = null!, Values = [1f] };
        Assert.Throws<ArgumentNullException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_NullValues_Throws()
    {
        var sparse = new SparseVector { Indices = [1u], Values = null! };
        Assert.Throws<ArgumentNullException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_IndicesLongerThanValues_Throws()
    {
        var sparse = new SparseVector { Indices = [1u, 2u], Values = [1f] };
        Assert.Throws<EdgeInvalidArgumentException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_ValuesLongerThanIndices_Throws()
    {
        var sparse = new SparseVector { Indices = [1u], Values = [1f, 2f] };
        Assert.Throws<EdgeInvalidArgumentException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_Empty_Passes()
    {
        var sparse = new SparseVector { Indices = [], Values = [] };
        sparse.Validate();
    }

    [Fact]
    public void SparseVector_PosInf_Throws()
    {
        var sparse = new SparseVector { Indices = [1u], Values = [float.PositiveInfinity] };
        Assert.Throws<EdgeInvalidArgumentException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_NegInf_Throws()
    {
        var sparse = new SparseVector { Indices = [1u], Values = [float.NegativeInfinity] };
        Assert.Throws<EdgeInvalidArgumentException>(sparse.Validate);
    }

    [Fact]
    public void SparseVector_RoundTripsThroughJson()
    {
        var sparse = new SparseVector { Indices = [1u, 5u, 9u], Values = [0.5f, 1.5f, 2.5f] };
        string json = JsonSerializer.Serialize(sparse, EdgeJsonContext.Deep.SparseVector);
        SparseVector? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SparseVector);
        Assert.NotNull(back);
        Assert.Equal([1u, 5u, 9u], back.Indices);
        Assert.Equal([0.5f, 1.5f, 2.5f], back.Values);
    }
}
