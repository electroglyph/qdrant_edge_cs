namespace Qdrant.Edge;

/// <summary>Payload selector: all, none, or a field list.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BoolWithPayload), "bool")]
[JsonDerivedType(typeof(FieldsWithPayload), "fields")]
[JsonDerivedType(typeof(ExcludeWithPayload), "exclude")]
public abstract record WithPayload
{
    /// <summary>Enables or disables all payload.</summary>
    /// <param name="enable">True to include payload.</param>
    public static WithPayload Bool(bool enable) => new BoolWithPayload(enable);

    /// <summary>Includes only the listed fields.</summary>
    /// <param name="fields">Payload keys.</param>
    public static WithPayload CreateFields(IReadOnlyList<string> fields) => new FieldsWithPayload(fields);

    /// <summary>Excludes the listed fields.</summary>
    /// <param name="fields">Payload keys.</param>
    public static WithPayload Exclude(IReadOnlyList<string> fields) => new ExcludeWithPayload(fields);
}

/// <summary>Boolean payload selector.</summary>
/// <param name="Enable">True to include payload.</param>
public sealed record BoolWithPayload(bool Enable) : WithPayload;

/// <summary>Field-inclusion payload selector.</summary>
/// <param name="Fields">Payload keys to include.</param>
public sealed record FieldsWithPayload(IReadOnlyList<string> Fields) : WithPayload;

/// <summary>Field-exclusion payload selector.</summary>
/// <param name="Fields">Payload keys to exclude.</param>
public sealed record ExcludeWithPayload(IReadOnlyList<string> Fields) : WithPayload;

/// <summary>Vector selector: all vectors or a name list.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BoolWithVector), "bool")]
[JsonDerivedType(typeof(NamesWithVector), "names")]
public abstract record WithVector
{
    /// <summary>Enables or disables all vectors.</summary>
    /// <param name="enable">True to include vectors.</param>
    public static WithVector Bool(bool enable) => new BoolWithVector(enable);

    /// <summary>Includes only the named vectors.</summary>
    /// <param name="names">Vector names.</param>
    public static WithVector CreateNames(IReadOnlyList<string> names) => new NamesWithVector(names);
}

/// <summary>Boolean vector selector.</summary>
/// <param name="Enable">True to include vectors.</param>
public sealed record BoolWithVector(bool Enable) : WithVector;

/// <summary>Named vector selector.</summary>
/// <param name="Names">Vector names to include.</param>
public sealed record NamesWithVector(IReadOnlyList<string> Names) : WithVector;
