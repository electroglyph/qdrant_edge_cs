namespace Qdrant.Edge;

/// <summary>Keyword index parameters.</summary>
public sealed record KeywordIndexParams
{
    /// <summary>Tenant key flag.</summary>
    public bool? IsTenant { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }

    /// <summary>Whether prefix search is enabled.</summary>
    public bool? Prefix { get; init; }
}

/// <summary>Integer index parameters.</summary>
public sealed record IntegerIndexParams
{
    /// <summary>Lookup index flag.</summary>
    public bool? Lookup { get; init; }

    /// <summary>Range index flag.</summary>
    public bool? Range { get; init; }

    /// <summary>Principal key flag.</summary>
    public bool? IsPrincipal { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }

    /// <summary>Validates the lookup/range combination.</summary>
    /// <exception cref="EdgeInvalidArgumentException">When both are disabled.</exception>
    public void Validate()
    {
        if (Lookup is false && Range is false)
        {
            throw new EdgeInvalidArgumentException("integer index must enable lookup, range, or both");
        }
    }
}

/// <summary>Float index parameters.</summary>
public sealed record FloatIndexParams
{
    /// <summary>Principal key flag.</summary>
    public bool? IsPrincipal { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>Geo index parameters.</summary>
public sealed record GeoIndexParams
{
    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>Text index parameters.</summary>
public sealed record TextIndexParams
{
    /// <summary>Tokenizer.</summary>
    public TokenizerType? Tokenizer { get; init; }

    /// <summary>Minimum token length.</summary>
    public ulong? MinTokenLen { get; init; }

    /// <summary>Maximum token length.</summary>
    public ulong? MaxTokenLen { get; init; }

    /// <summary>Whether to lowercase tokens.</summary>
    public bool? Lowercase { get; init; }

    /// <summary>Whether to fold ASCII.</summary>
    public bool? AsciiFolding { get; init; }

    /// <summary>Whether phrase matching is enabled.</summary>
    public bool? PhraseMatching { get; init; }

    /// <summary>Stopword configuration.</summary>
    public Stopwords? Stopwords { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Stemmer configuration.</summary>
    public Stemmer? Stemmer { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>Boolean index parameters.</summary>
public sealed record BoolIndexParams
{
    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>Datetime index parameters.</summary>
public sealed record DatetimeIndexParams
{
    /// <summary>Principal key flag.</summary>
    public bool? IsPrincipal { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>UUID index parameters.</summary>
public sealed record UuidIndexParams
{
    /// <summary>Tenant key flag.</summary>
    public bool? IsTenant { get; init; }

    /// <summary>Memory placement.</summary>
    public Memory? Memory { get; init; }

    /// <summary>Whether to build HNSW over the index.</summary>
    public bool? EnableHnsw { get; init; }
}

/// <summary>Payload index parameters union (8 index types).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(KeywordPayloadIndex), "keyword")]
[JsonDerivedType(typeof(IntegerPayloadIndex), "integer")]
[JsonDerivedType(typeof(FloatPayloadIndex), "float")]
[JsonDerivedType(typeof(GeoPayloadIndex), "geo")]
[JsonDerivedType(typeof(TextPayloadIndex), "text")]
[JsonDerivedType(typeof(BoolPayloadIndex), "bool")]
[JsonDerivedType(typeof(DatetimePayloadIndex), "datetime")]
[JsonDerivedType(typeof(UuidPayloadIndex), "uuid")]
public abstract record PayloadIndexParams
{
    /// <summary>Creates keyword params.</summary>
    /// <param name="config">Keyword config.</param>
    public static PayloadIndexParams Keyword(KeywordIndexParams config) => new KeywordPayloadIndex(config);

    /// <summary>Creates integer params.</summary>
    /// <param name="config">Integer config.</param>
    public static PayloadIndexParams Integer(IntegerIndexParams config) => new IntegerPayloadIndex(config);

    /// <summary>Creates float params.</summary>
    /// <param name="config">Float config.</param>
    public static PayloadIndexParams Float(FloatIndexParams config) => new FloatPayloadIndex(config);

    /// <summary>Creates geo params.</summary>
    /// <param name="config">Geo config.</param>
    public static PayloadIndexParams Geo(GeoIndexParams config) => new GeoPayloadIndex(config);

    /// <summary>Creates text params.</summary>
    /// <param name="config">Text config.</param>
    public static PayloadIndexParams Text(TextIndexParams config) => new TextPayloadIndex(config);

    /// <summary>Creates boolean params.</summary>
    /// <param name="config">Boolean config.</param>
    public static PayloadIndexParams Bool(BoolIndexParams config) => new BoolPayloadIndex(config);

    /// <summary>Creates datetime params.</summary>
    /// <param name="config">Datetime config.</param>
    public static PayloadIndexParams Datetime(DatetimeIndexParams config) => new DatetimePayloadIndex(config);

    /// <summary>Creates UUID params.</summary>
    /// <param name="config">UUID config.</param>
    public static PayloadIndexParams Uuid(UuidIndexParams config) => new UuidPayloadIndex(config);
}

/// <summary>Keyword payload index.</summary>
/// <param name="Config">Keyword config.</param>
public sealed record KeywordPayloadIndex(KeywordIndexParams Config) : PayloadIndexParams;

/// <summary>Integer payload index.</summary>
/// <param name="Config">Integer config.</param>
public sealed record IntegerPayloadIndex(IntegerIndexParams Config) : PayloadIndexParams;

/// <summary>Float payload index.</summary>
/// <param name="Config">Float config.</param>
public sealed record FloatPayloadIndex(FloatIndexParams Config) : PayloadIndexParams;

/// <summary>Geo payload index.</summary>
/// <param name="Config">Geo config.</param>
public sealed record GeoPayloadIndex(GeoIndexParams Config) : PayloadIndexParams;

/// <summary>Text payload index.</summary>
/// <param name="Config">Text config.</param>
public sealed record TextPayloadIndex(TextIndexParams Config) : PayloadIndexParams;

/// <summary>Boolean payload index.</summary>
/// <param name="Config">Boolean config.</param>
public sealed record BoolPayloadIndex(BoolIndexParams Config) : PayloadIndexParams;

/// <summary>Datetime payload index.</summary>
/// <param name="Config">Datetime config.</param>
public sealed record DatetimePayloadIndex(DatetimeIndexParams Config) : PayloadIndexParams;

/// <summary>UUID payload index.</summary>
/// <param name="Config">UUID config.</param>
public sealed record UuidPayloadIndex(UuidIndexParams Config) : PayloadIndexParams;

/// <summary>Payload index report entry.</summary>
public sealed record PayloadIndexInfo
{
    /// <summary>Index data type.</summary>
    public required PayloadSchemaType DataType { get; init; }

    /// <summary>Creation parameters, when supplied.</summary>
    public PayloadIndexParams? Params { get; init; }

    /// <summary>Number of indexed points.</summary>
    public required ulong Points { get; init; }
}
