namespace Qdrant.Edge;

/// <summary>Single payload value variant.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StringValueVariant), "string")]
[JsonDerivedType(typeof(IntegerValueVariant), "integer")]
[JsonDerivedType(typeof(BoolValueVariant), "bool")]
public abstract record ValueVariants
{
    /// <summary>Creates a string value.</summary>
    /// <param name="value">String value.</param>
    public static ValueVariants String(string value) => new StringValueVariant(value);

    /// <summary>Creates an integer value.</summary>
    /// <param name="value">Integer value.</param>
    public static ValueVariants Integer(long value) => new IntegerValueVariant(value);

    /// <summary>Creates a boolean value.</summary>
    /// <param name="value">Boolean value.</param>
    public static ValueVariants Bool(bool value) => new BoolValueVariant(value);
}

/// <summary>String value.</summary>
/// <param name="Value">String value.</param>
public sealed record StringValueVariant(string Value) : ValueVariants;

/// <summary>Integer value.</summary>
/// <param name="Value">Integer value.</param>
public sealed record IntegerValueVariant(long Value) : ValueVariants;

/// <summary>Boolean value.</summary>
/// <param name="Value">Boolean value.</param>
public sealed record BoolValueVariant(bool Value) : ValueVariants;

/// <summary>Multi-value variant for Any/Except matches.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StringsAnyVariant), "strings")]
[JsonDerivedType(typeof(IntegersAnyVariant), "integers")]
public abstract record AnyVariants
{
    /// <summary>Creates a string list variant.</summary>
    /// <param name="values">String values.</param>
    public static AnyVariants Strings(IReadOnlyList<string> values) => new StringsAnyVariant(values);

    /// <summary>Creates an integer list variant.</summary>
    /// <param name="values">Integer values.</param>
    public static AnyVariants Integers(IReadOnlyList<long> values) => new IntegersAnyVariant(values);
}

/// <summary>String list variant.</summary>
/// <param name="Values">String values.</param>
public sealed record StringsAnyVariant(IReadOnlyList<string> Values) : AnyVariants;

/// <summary>Integer list variant.</summary>
/// <param name="Values">Integer values.</param>
public sealed record IntegersAnyVariant(IReadOnlyList<long> Values) : AnyVariants;

/// <summary>Match predicate: single enum with Value/Text/TextAny/Phrase/Prefix/Substring/Any/Except.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ValueMatch), "value")]
[JsonDerivedType(typeof(TextMatch), "text")]
[JsonDerivedType(typeof(TextAnyMatch), "text_any")]
[JsonDerivedType(typeof(PhraseMatch), "phrase")]
[JsonDerivedType(typeof(PrefixMatch), "prefix")]
[JsonDerivedType(typeof(SubstringMatch), "substring")]
[JsonDerivedType(typeof(AnyMatch), "any")]
[JsonDerivedType(typeof(ExceptMatch), "except")]
public abstract record Match
{
    /// <summary>Exact value match.</summary>
    /// <param name="value">Value to match.</param>
    public static Match CreateValue(ValueVariants value) => new ValueMatch(value);

    /// <summary>Full-text match.</summary>
    /// <param name="text">Query text.</param>
    public static Match CreateText(string text) => new TextMatch(text);

    /// <summary>Match any of the space-separated tokens.</summary>
    /// <param name="textAny">Tokens text.</param>
    public static Match CreateTextAny(string textAny) => new TextAnyMatch(textAny);

    /// <summary>Phrase match.</summary>
    /// <param name="phrase">Phrase.</param>
    public static Match CreatePhrase(string phrase) => new PhraseMatch(phrase);

    /// <summary>Prefix match.</summary>
    /// <param name="prefix">Prefix.</param>
    public static Match CreatePrefix(string prefix) => new PrefixMatch(prefix);

    /// <summary>Substring match.</summary>
    /// <param name="substring">Substring.</param>
    public static Match CreateSubstring(string substring) => new SubstringMatch(substring);

    /// <summary>Match any listed value (empty is valid).</summary>
    /// <param name="any">Allowed values.</param>
    public static Match CreateAny(AnyVariants any) => new AnyMatch(any);

    /// <summary>Match anything except the listed values.</summary>
    /// <param name="except">Excluded values.</param>
    public static Match CreateExcept(AnyVariants except) => new ExceptMatch(except);
}

/// <summary>Exact value match.</summary>
/// <param name="Value">Value to match.</param>
public sealed record ValueMatch(ValueVariants Value) : Match;

/// <summary>Full-text match.</summary>
/// <param name="Text">Query text.</param>
public sealed record TextMatch(string Text) : Match;

/// <summary>Token-any match.</summary>
/// <param name="TextAny">Tokens text.</param>
public sealed record TextAnyMatch(string TextAny) : Match;

/// <summary>Phrase match.</summary>
/// <param name="Phrase">Phrase.</param>
public sealed record PhraseMatch(string Phrase) : Match;

/// <summary>Prefix match.</summary>
/// <param name="Prefix">Prefix.</param>
public sealed record PrefixMatch(string Prefix) : Match;

/// <summary>Substring match.</summary>
/// <param name="Substring">Substring.</param>
public sealed record SubstringMatch(string Substring) : Match;

/// <summary>Any-of match.</summary>
/// <param name="Any">Allowed values.</param>
public sealed record AnyMatch(AnyVariants Any) : Match;

/// <summary>Except match (C# name avoids the <c>except</c> keyword clash).</summary>
/// <param name="Except">Excluded values.</param>
public sealed record ExceptMatch(AnyVariants Except) : Match;
