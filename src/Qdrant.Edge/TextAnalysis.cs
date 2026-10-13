namespace Qdrant.Edge;

/// <summary>Tokenizer type for text indexes.</summary>
public enum TokenizerType
{
    /// <summary>Prefix tokenizer.</summary>
    Prefix,

    /// <summary>Whitespace tokenizer.</summary>
    Whitespace,

    /// <summary>Word tokenizer.</summary>
    Word,

    /// <summary>Multilingual tokenizer.</summary>
    Multilingual,
}

/// <summary>Stopword source language (30 values, FFI order).</summary>
public enum Language
{
    /// <summary>Arabic.</summary>
    Arabic,

    /// <summary>Azerbaijani.</summary>
    Azerbaijani,

    /// <summary>Basque.</summary>
    Basque,

    /// <summary>Bengali.</summary>
    Bengali,

    /// <summary>Catalan.</summary>
    Catalan,

    /// <summary>Chinese.</summary>
    Chinese,

    /// <summary>Danish.</summary>
    Danish,

    /// <summary>Dutch.</summary>
    Dutch,

    /// <summary>English.</summary>
    English,

    /// <summary>Finnish.</summary>
    Finnish,

    /// <summary>French.</summary>
    French,

    /// <summary>German.</summary>
    German,

    /// <summary>Greek.</summary>
    Greek,

    /// <summary>Hebrew.</summary>
    Hebrew,

    /// <summary>Hinglish.</summary>
    Hinglish,

    /// <summary>Hungarian.</summary>
    Hungarian,

    /// <summary>Indonesian.</summary>
    Indonesian,

    /// <summary>Italian.</summary>
    Italian,

    /// <summary>Japanese.</summary>
    Japanese,

    /// <summary>Kazakh.</summary>
    Kazakh,

    /// <summary>Nepali.</summary>
    Nepali,

    /// <summary>Norwegian.</summary>
    Norwegian,

    /// <summary>Portuguese.</summary>
    Portuguese,

    /// <summary>Romanian.</summary>
    Romanian,

    /// <summary>Russian.</summary>
    Russian,

    /// <summary>Slovene.</summary>
    Slovene,

    /// <summary>Spanish.</summary>
    Spanish,

    /// <summary>Swedish.</summary>
    Swedish,

    /// <summary>Tajik.</summary>
    Tajik,

    /// <summary>Turkish.</summary>
    Turkish,
}

/// <summary>Snowball stemmer language (19 values, FFI order).</summary>
public enum SnowballLanguage
{
    /// <summary>Arabic.</summary>
    Arabic,

    /// <summary>Armenian.</summary>
    Armenian,

    /// <summary>Danish.</summary>
    Danish,

    /// <summary>Dutch.</summary>
    Dutch,

    /// <summary>English.</summary>
    English,

    /// <summary>Finnish.</summary>
    Finnish,

    /// <summary>French.</summary>
    French,

    /// <summary>German.</summary>
    German,

    /// <summary>Greek.</summary>
    Greek,

    /// <summary>Hungarian.</summary>
    Hungarian,

    /// <summary>Italian.</summary>
    Italian,

    /// <summary>Norwegian.</summary>
    Norwegian,

    /// <summary>Portuguese.</summary>
    Portuguese,

    /// <summary>Romanian.</summary>
    Romanian,

    /// <summary>Russian.</summary>
    Russian,

    /// <summary>Spanish.</summary>
    Spanish,

    /// <summary>Swedish.</summary>
    Swedish,

    /// <summary>Tamil.</summary>
    Tamil,

    /// <summary>Turkish.</summary>
    Turkish,
}

/// <summary>Stopword configuration.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LanguageStopwords), "language")]
[JsonDerivedType(typeof(SetStopwords), "set")]
public abstract record Stopwords
{
    /// <summary>Creates a language stopword set.</summary>
    /// <param name="language">Language.</param>
    public static Stopwords CreateLanguage(Language language) => new LanguageStopwords(language);

    /// <summary>Creates a custom stopword set.</summary>
    /// <param name="languages">Optional languages.</param>
    /// <param name="custom">Optional custom tokens.</param>
    public static Stopwords Set(IReadOnlyList<Language>? languages, IReadOnlyList<string>? custom) => new SetStopwords(languages, custom);
}

/// <summary>Language stopwords.</summary>
/// <param name="Language">Language.</param>
public sealed record LanguageStopwords(Language Language) : Stopwords;

/// <summary>Custom stopword set.</summary>
/// <param name="Languages">Optional languages.</param>
/// <param name="Custom">Optional custom tokens.</param>
public sealed record SetStopwords(IReadOnlyList<Language>? Languages, IReadOnlyList<string>? Custom) : Stopwords;

/// <summary>Stemmer configuration.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SnowballStemmer), "snowball")]
[JsonDerivedType(typeof(DisabledStemmer), "disabled")]
public abstract record Stemmer
{
    /// <summary>Creates a Snowball stemmer.</summary>
    /// <param name="language">Stemmer language.</param>
    public static Stemmer Snowball(SnowballLanguage language) => new SnowballStemmer(language);

    /// <summary>Creates a disabled stemmer.</summary>
    public static Stemmer Disabled() => new DisabledStemmer();
}

/// <summary>Snowball stemmer.</summary>
/// <param name="Language">Stemmer language.</param>
public sealed record SnowballStemmer(SnowballLanguage Language) : Stemmer;

/// <summary>Disabled stemmer.</summary>
public sealed record DisabledStemmer : Stemmer;
