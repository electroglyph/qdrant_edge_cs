namespace Qdrant.Edge;

/// <summary>Recommend strategy.</summary>
public enum RecommendStrategy
{
    /// <summary>Best-score strategy.</summary>
    BestScore,

    /// <summary>Sum-scores strategy.</summary>
    SumScores,
}

/// <summary>Context pair for discover/context queries.</summary>
public sealed record ContextPair
{
    /// <summary>Positive example.</summary>
    public required NamedVector Positive { get; init; }

    /// <summary>Negative example.</summary>
    public required NamedVector Negative { get; init; }
}

/// <summary>Feedback item with an explicit score.</summary>
public sealed record FeedbackItem
{
    /// <summary>Feedback vector.</summary>
    public required NamedVector Vector { get; init; }

    /// <summary>Feedback score; must be finite.</summary>
    public required float Score { get; init; }
}

/// <summary>Feedback combination coefficients.</summary>
public sealed record FeedbackCoefficients
{
    /// <summary>Coefficient a.</summary>
    public required float A { get; init; }

    /// <summary>Coefficient b.</summary>
    public required float B { get; init; }

    /// <summary>Coefficient c.</summary>
    public required float C { get; init; }
}

/// <summary>Vector-side query: Nearest/Recommend/Discover/Context/Feedback.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NearestQuery), "nearest")]
[JsonDerivedType(typeof(RecommendQuery), "recommend")]
[JsonDerivedType(typeof(DiscoverQuery), "discover")]
[JsonDerivedType(typeof(ContextQuery), "context")]
[JsonDerivedType(typeof(FeedbackQuery), "feedback")]
public abstract record Query
{
    /// <summary>Nearest-neighbor query.</summary>
    /// <param name="vector">Query vector.</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static Query Nearest(NamedVector vector, string? usingVector = null) => new NearestQuery(vector, usingVector);

    /// <summary>Recommend query.</summary>
    /// <param name="positives">Positive examples.</param>
    /// <param name="negatives">Negative examples.</param>
    /// <param name="strategy">Strategy (default BestScore).</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static Query Recommend(
        IReadOnlyList<NamedVector> positives,
        IReadOnlyList<NamedVector> negatives,
        RecommendStrategy? strategy = null,
        string? usingVector = null) => new RecommendQuery(positives, negatives, strategy, usingVector);

    /// <summary>Discover query.</summary>
    /// <param name="target">Target vector.</param>
    /// <param name="context">Context pairs.</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static Query Discover(NamedVector target, IReadOnlyList<ContextPair> context, string? usingVector = null) => new DiscoverQuery(target, context, usingVector);

    /// <summary>Context query.</summary>
    /// <param name="context">Context pairs.</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static Query CreateContext(IReadOnlyList<ContextPair> context, string? usingVector = null) => new ContextQuery(context, usingVector);

    /// <summary>Feedback query.</summary>
    /// <param name="target">Target vector.</param>
    /// <param name="feedback">Feedback items.</param>
    /// <param name="coefficients">Combination coefficients.</param>
    /// <param name="usingVector">Optional vector name.</param>
    public static Query CreateFeedback(
        NamedVector target,
        IReadOnlyList<FeedbackItem> feedback,
        FeedbackCoefficients coefficients,
        string? usingVector = null) => new FeedbackQuery(target, feedback, coefficients, usingVector);

    /// <summary>Validates query vectors, finiteness, and nested shapes.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When the shape is invalid.</exception>
    public void Validate()
    {
        switch (this)
        {
            case NearestQuery(var vector, _):
                ArgumentNullException.ThrowIfNull(vector, nameof(vector));
                vector.Validate();
                break;
            case RecommendQuery(var positives, var negatives, _, _):
                ArgumentNullException.ThrowIfNull(positives, nameof(positives));
                ArgumentNullException.ThrowIfNull(negatives, nameof(negatives));
                if (positives.Count == 0)
                {
                    throw new EdgeInvalidArgumentException("recommend query requires at least one positive example");
                }

                foreach (NamedVector vector in positives)
                {
                    ArgumentNullException.ThrowIfNull(vector, nameof(vector));
                    vector.Validate();
                }

                foreach (NamedVector vector in negatives)
                {
                    ArgumentNullException.ThrowIfNull(vector, nameof(vector));
                    vector.Validate();
                }

                break;
            case DiscoverQuery(var target, var context, _):
                ArgumentNullException.ThrowIfNull(target, nameof(target));
                ArgumentNullException.ThrowIfNull(context, nameof(context));
                target.Validate();
                foreach (ContextPair pair in context)
                {
                    ArgumentNullException.ThrowIfNull(pair, nameof(pair));
                    ArgumentNullException.ThrowIfNull(pair.Positive, nameof(pair.Positive));
                    ArgumentNullException.ThrowIfNull(pair.Negative, nameof(pair.Negative));
                    pair.Positive.Validate();
                    pair.Negative.Validate();
                }

                break;
            case ContextQuery(var context, _):
                ArgumentNullException.ThrowIfNull(context, nameof(context));
                foreach (ContextPair pair in context)
                {
                    ArgumentNullException.ThrowIfNull(pair, nameof(pair));
                    ArgumentNullException.ThrowIfNull(pair.Positive, nameof(pair.Positive));
                    ArgumentNullException.ThrowIfNull(pair.Negative, nameof(pair.Negative));
                    pair.Positive.Validate();
                    pair.Negative.Validate();
                }

                break;
            case FeedbackQuery(var target, var feedback, var coefficients, _):
                ArgumentNullException.ThrowIfNull(target, nameof(target));
                ArgumentNullException.ThrowIfNull(feedback, nameof(feedback));
                ArgumentNullException.ThrowIfNull(coefficients, nameof(coefficients));
                target.Validate();
                foreach (FeedbackItem item in feedback)
                {
                    ArgumentNullException.ThrowIfNull(item, nameof(item));
                    ArgumentNullException.ThrowIfNull(item.Vector, nameof(item.Vector));
                    item.Vector.Validate();
                    EdgeValidation.CheckFinite("feedback score", item.Score);
                }

                EdgeValidation.CheckFinite("feedback coefficient a", coefficients.A);
                EdgeValidation.CheckFinite("feedback coefficient b", coefficients.B);
                EdgeValidation.CheckFinite("feedback coefficient c", coefficients.C);
                break;
            default:
                throw new EdgeInvalidArgumentException("unknown query shape");
        }
    }
}

/// <summary>Nearest query.</summary>
/// <param name="Vector">Query vector.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record NearestQuery(NamedVector Vector, string? Using) : Query;

/// <summary>Recommend query.</summary>
/// <param name="Positives">Positive examples.</param>
/// <param name="Negatives">Negative examples.</param>
/// <param name="Strategy">Strategy.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record RecommendQuery(
    IReadOnlyList<NamedVector> Positives,
    IReadOnlyList<NamedVector> Negatives,
    RecommendStrategy? Strategy,
    string? Using) : Query;

/// <summary>Discover query.</summary>
/// <param name="Target">Target vector.</param>
/// <param name="Context">Context pairs.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record DiscoverQuery(NamedVector Target, IReadOnlyList<ContextPair> Context, string? Using) : Query;

/// <summary>Context query.</summary>
/// <param name="Context">Context pairs.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record ContextQuery(IReadOnlyList<ContextPair> Context, string? Using) : Query;

/// <summary>Feedback query.</summary>
/// <param name="Target">Target vector.</param>
/// <param name="Feedback">Feedback items.</param>
/// <param name="Coefficients">Combination coefficients.</param>
/// <param name="Using">Optional vector name.</param>
public sealed record FeedbackQuery(
    NamedVector Target,
    IReadOnlyList<FeedbackItem> Feedback,
    FeedbackCoefficients Coefficients,
    string? Using) : Query;

/// <summary>Fusion strategy.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(RrfFusion), "rrf")]
[JsonDerivedType(typeof(DbsfFusion), "dbsf")]
public abstract record Fusion
{
    /// <summary>Reciprocal-rank fusion.</summary>
    /// <param name="k">Rank constant.</param>
    /// <param name="weights">Optional per-source weights.</param>
    public static Fusion Rrf(ulong k, IReadOnlyList<float>? weights = null) => new RrfFusion(k, weights);

    /// <summary>Distribution-based score fusion.</summary>
    public static Fusion Dbsf() => new DbsfFusion();
}

/// <summary>RRF fusion.</summary>
/// <param name="K">Rank constant.</param>
/// <param name="Weights">Optional per-source weights.</param>
public sealed record RrfFusion(ulong K, IReadOnlyList<float>? Weights) : Fusion;

/// <summary>DBSF fusion.</summary>
public sealed record DbsfFusion : Fusion;

/// <summary>Sample strategy.</summary>
public enum Sample
{
    /// <summary>Random sampling.</summary>
    Random,
}

/// <summary>Sort direction.</summary>
public enum Direction
{
    /// <summary>Ascending.</summary>
    Asc,

    /// <summary>Descending.</summary>
    Desc,
}

/// <summary>Order-by start position.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(IntegerStartFrom), "integer")]
[JsonDerivedType(typeof(FloatStartFrom), "float")]
[JsonDerivedType(typeof(DatetimeStartFrom), "datetime")]
public abstract record StartFrom;

/// <summary>Integer start position.</summary>
/// <param name="Value">Start value.</param>
public sealed record IntegerStartFrom(long Value) : StartFrom;

/// <summary>Float start position.</summary>
/// <param name="Value">Start value.</param>
public sealed record FloatStartFrom(double Value) : StartFrom;

/// <summary>Datetime start position (RFC3339).</summary>
/// <param name="Value">Start value.</param>
public sealed record DatetimeStartFrom(string Value) : StartFrom;

/// <summary>Order-by clause.</summary>
public sealed record OrderBy
{
    /// <summary>Payload key to sort by.</summary>
    public required string Key { get; init; }

    /// <summary>Sort direction.</summary>
    public Direction? Direction { get; init; }

    /// <summary>Start position.</summary>
    public StartFrom? StartFrom { get; init; }
}

/// <summary>Decay kind for formula decay expressions.</summary>
public enum DecayKind
{
    /// <summary>Linear decay.</summary>
    Lin,

    /// <summary>Gaussian decay.</summary>
    Gauss,

    /// <summary>Exponential decay.</summary>
    Exp,
}
