namespace Qdrant.Edge;

/// <summary>Scoring query: Vector/Fusion/OrderBy/Formula/Mmr/Sample/Text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(VectorScoringQuery), "vector")]
[JsonDerivedType(typeof(FusionScoringQuery), "fusion")]
[JsonDerivedType(typeof(OrderByScoringQuery), "order_by")]
[JsonDerivedType(typeof(FormulaScoringQuery), "formula")]
[JsonDerivedType(typeof(MmrScoringQuery), "mmr")]
[JsonDerivedType(typeof(SampleScoringQuery), "sample")]
[JsonDerivedType(typeof(TextScoringQuery), "text")]
public abstract record ScoringQuery
{
    /// <summary>Creates a vector scoring query.</summary>
    /// <param name="query">Vector query.</param>
    public static ScoringQuery CreateVector(Query query) => new VectorScoringQuery(query);

    /// <summary>Creates a fusion scoring query.</summary>
    /// <param name="fusion">Fusion strategy.</param>
    public static ScoringQuery CreateFusion(Fusion fusion) => new FusionScoringQuery(fusion);

    /// <summary>Creates an order-by scoring query.</summary>
    /// <param name="orderBy">Order-by clause.</param>
    public static ScoringQuery CreateOrderBy(OrderBy orderBy) => new OrderByScoringQuery(orderBy);

    /// <summary>Creates a formula scoring query.</summary>
    /// <param name="expression">Formula expression.</param>
    /// <param name="defaults">Default variables.</param>
    public static ScoringQuery Formula(Expression expression, IReadOnlyDictionary<string, string>? defaults = null) => new FormulaScoringQuery(expression, defaults);

    /// <summary>Creates a maximal-marginal-relevance query (a variant, not a standalone class).</summary>
    /// <param name="vector">Query vector.</param>
    /// <param name="lambda">Diversity trade-off in [0, 1].</param>
    /// <param name="candidatesLimit">Candidate pool size.</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static ScoringQuery Mmr(NamedVector vector, float lambda, ulong candidatesLimit, string? usingVector = null) => new MmrScoringQuery(vector, lambda, candidatesLimit, usingVector);

    /// <summary>Creates a sampling query.</summary>
    /// <param name="sample">Sample strategy.</param>
    public static ScoringQuery CreateSample(Sample sample) => new SampleScoringQuery(sample);

    /// <summary>Creates a BM25 text scoring query over a text-indexed field.</summary>
    /// <param name="field">Payload field with a scoring text index.</param>
    /// <param name="query">Text to search for.</param>
    /// <param name="scoring">Optional scorer parameters.</param>
    public static ScoringQuery CreateText(string field, string query, TextQueryScoring? scoring = null) => new TextScoringQuery(field, query, scoring);

    /// <summary>Validates nested query, formula, and mmr shapes.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case VectorScoringQuery(var query):
                ArgumentNullException.ThrowIfNull(query, nameof(query));
                query.Validate();
                break;
            case FusionScoringQuery(var fusion):
                ArgumentNullException.ThrowIfNull(fusion, nameof(fusion));
                if (fusion is RrfFusion(var k, var weights) && weights is not null)
                {
                    foreach (float weight in weights)
                    {
                        EdgeValidation.CheckFinite("rrf weight", weight);
                    }
                }

                break;
            case OrderByScoringQuery(var orderBy):
                ArgumentNullException.ThrowIfNull(orderBy, nameof(orderBy));
                ArgumentException.ThrowIfNullOrWhiteSpace(orderBy.Key, nameof(orderBy.Key));
                EdgeValidation.CheckPayloadKey(orderBy.Key);
                if (orderBy.StartFrom is FloatStartFrom(var value))
                {
                    EdgeValidation.CheckFinite("order-by start_from", value);
                }

                break;
            case FormulaScoringQuery(var expression, _):
                ArgumentNullException.ThrowIfNull(expression, nameof(expression));
                expression.Validate();
                break;
            case MmrScoringQuery mmr:
                ArgumentNullException.ThrowIfNull(mmr.Vector, nameof(mmr.Vector));
                mmr.Vector.Validate();
                mmr.ValidateMmr();
                break;
            case SampleScoringQuery:
                break;
            case TextScoringQuery(var field, var query, var scoring):
                ArgumentException.ThrowIfNullOrWhiteSpace(field, nameof(field));
                ArgumentException.ThrowIfNullOrWhiteSpace(query, nameof(query));
                EdgeValidation.CheckPayloadKey(field);
                scoring?.Validate();
                break;
            default:
                throw new EdgeInvalidArgumentException("unknown scoring query shape");
        }
    }
}

/// <summary>Vector scoring query.</summary>
/// <param name="Query">Vector query.</param>
public sealed record VectorScoringQuery(Query Query) : ScoringQuery;

/// <summary>Fusion scoring query.</summary>
/// <param name="Fusion">Fusion strategy.</param>
public sealed record FusionScoringQuery(Fusion Fusion) : ScoringQuery;

/// <summary>Order-by scoring query.</summary>
/// <param name="OrderBy">Order-by clause.</param>
public sealed record OrderByScoringQuery(OrderBy OrderBy) : ScoringQuery;

/// <summary>Formula scoring query.</summary>
/// <param name="Expression">Formula expression.</param>
/// <param name="Defaults">Default variables.</param>
public sealed record FormulaScoringQuery(Expression Expression, IReadOnlyDictionary<string, string>? Defaults) : ScoringQuery;

/// <summary>MMR scoring query variant.</summary>
/// <param name="Vector">Query vector.</param>
/// <param name="Lambda">Diversity trade-off in [0, 1].</param>
/// <param name="CandidatesLimit">Candidate pool size.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record MmrScoringQuery(NamedVector Vector, float Lambda, ulong CandidatesLimit, string? Using) : ScoringQuery
{
    /// <summary>Validates lambda range and candidate cap.</summary>
    public void ValidateMmr()
    {
        EdgeValidation.CheckFinite(nameof(Lambda), Lambda);
        if (Lambda is < 0f or > 1f)
        {
            throw new EdgeInvalidArgumentException($"mmr lambda {Lambda} must be in [0, 1]");
        }

        EdgeValidation.CheckResultCount("candidates_limit", CandidatesLimit);
    }
}

/// <summary>Sample scoring query.</summary>
/// <param name="Sample">Sample strategy.</param>
public sealed record SampleScoringQuery(Sample Sample) : ScoringQuery;

/// <summary>BM25 parameters for a text scoring query.</summary>
public sealed record Bm25Params
{
    /// <summary>Term frequency saturation, non-negative.</summary>
    public float? K { get; init; }

    /// <summary>Document length normalization in [0, 1].</summary>
    public float? B { get; init; }

    /// <summary>Validates finiteness and documented ranges.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When a bound is violated.</exception>
    public void Validate()
    {
        if (K is float k)
        {
            EdgeValidation.CheckFinite("bm25 k", k);
            if (k < 0f)
            {
                throw new EdgeInvalidArgumentException($"bm25 k {k} must be non-negative");
            }
        }

        if (B is float b)
        {
            EdgeValidation.CheckFinite("bm25 b", b);
            if (b is < 0f or > 1f)
            {
                throw new EdgeInvalidArgumentException($"bm25 b {b} must be in [0, 1]");
            }
        }
    }
}

/// <summary>Scorer for a text query.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Bm25TextScoring), "bm25")]
public abstract record TextQueryScoring
{
    /// <summary>Creates BM25 scoring.</summary>
    /// <param name="params">BM25 parameters.</param>
    public static TextQueryScoring Bm25(Bm25Params @params) => new Bm25TextScoring(@params);

    /// <summary>Validates nested scorer parameters.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case Bm25TextScoring(var @params):
                ArgumentNullException.ThrowIfNull(@params, nameof(@params));
                @params.Validate();
                break;
            default:
                throw new EdgeInvalidArgumentException("unknown text scoring shape");
        }
    }
}

/// <summary>BM25 text scoring.</summary>
/// <param name="Params">BM25 parameters.</param>
public sealed record Bm25TextScoring(Bm25Params Params) : TextQueryScoring;

/// <summary>Text scoring query ranking by BM25 over a text index.</summary>
/// <param name="Field">Payload field with a scoring text index.</param>
/// <param name="Query">Text to search for.</param>
/// <param name="Scoring">Optional scorer parameters.</param>
public sealed record TextScoringQuery(string Field, string Query, TextQueryScoring? Scoring) : ScoringQuery;

/// <summary>Search tuning parameters (FFI shape: hnsw_ef/exact/indexed_only only).</summary>
public sealed record SearchParams
{
    /// <summary>HNSW ef tuning knob.</summary>
    public ulong? HnswEf { get; init; }

    /// <summary>Whether to run an exact scan.</summary>
    public bool Exact { get; init; }

    /// <summary>Whether to use indexed vectors only.</summary>
    public bool IndexedOnly { get; init; }
}
