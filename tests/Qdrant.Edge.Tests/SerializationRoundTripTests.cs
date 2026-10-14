using System.Text.Json;

namespace Qdrant.Edge.Tests;

/// <summary>JSON round-trip pins for Quantization, Range, and TextAnalysis DTOs.</summary>
public sealed class SerializationRoundTripTests
{
    public static IEnumerable<object[]> AllQuantizationKinds()
    {
        yield return new object[] { QuantizationConfig.Scalar(new ScalarQuantizationParams { Type = ScalarType.Int8 }) };
        yield return new object[] { QuantizationConfig.Product(new ProductQuantizationParams { Compression = CompressionRatio.X16 }) };
        yield return new object[] { QuantizationConfig.Binary(new BinaryQuantizationParams { }) };
        yield return new object[] { QuantizationConfig.Turbo(new TurboQuantizationParams { }) };
    }

    [Theory]
    [MemberData(nameof(AllQuantizationKinds))]
    public void QuantizationConfig_AllFourKinds_RoundTripThroughJson(QuantizationConfig original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.QuantizationConfig);
        QuantizationConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.QuantizationConfig);
        Assert.Equal(original, back);
        Assert.IsType(original.GetType(), back);
    }

    [Fact]
    public void ScalarQuantizationParams_Fields_Preserved()
    {
        var original = new ScalarQuantizationParams { Type = ScalarType.Int8, Quantile = 0.75f, Memory = Memory.Cached };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScalarQuantizationParams);
        ScalarQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScalarQuantizationParams);
        Assert.NotNull(back);
        Assert.Equal(original.Type, back.Type);
        Assert.Equal(original.Quantile, back.Quantile);
        Assert.Equal(original.Memory, back.Memory);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ScalarQuantizationParams_NullOptionals_Preserved()
    {
        var original = new ScalarQuantizationParams { Type = ScalarType.Int8, Quantile = null, Memory = null };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScalarQuantizationParams);
        ScalarQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScalarQuantizationParams);
        Assert.NotNull(back);
        Assert.Null(back.Quantile);
        Assert.Null(back.Memory);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ProductQuantizationParams_Compression_Preserved()
    {
        var original = new ProductQuantizationParams { Compression = CompressionRatio.X16, Memory = Memory.Pinned };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ProductQuantizationParams);
        ProductQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ProductQuantizationParams);
        Assert.NotNull(back);
        Assert.Equal(original.Compression, back.Compression);
        Assert.Equal(original.Memory, back.Memory);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(BinaryQuantizationEncoding.OneBit)]
    [InlineData(BinaryQuantizationEncoding.TwoBits)]
    [InlineData(BinaryQuantizationEncoding.OneAndHalfBits)]
    public void BinaryQuantizationParams_Encodings_Preserved(BinaryQuantizationEncoding encoding)
    {
        var original = new BinaryQuantizationParams { Encoding = encoding };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.BinaryQuantizationParams);
        BinaryQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.BinaryQuantizationParams);
        Assert.NotNull(back);
        Assert.Equal(encoding, back.Encoding);
    }

    [Theory]
    [InlineData(BinaryQuantizationQueryEncoding.Default)]
    [InlineData(BinaryQuantizationQueryEncoding.Binary)]
    [InlineData(BinaryQuantizationQueryEncoding.Scalar4Bits)]
    [InlineData(BinaryQuantizationQueryEncoding.Scalar8Bits)]
    public void BinaryQuantizationParams_QueryEncodings_Preserved(BinaryQuantizationQueryEncoding queryEncoding)
    {
        var original = new BinaryQuantizationParams { QueryEncoding = queryEncoding };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.BinaryQuantizationParams);
        BinaryQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.BinaryQuantizationParams);
        Assert.NotNull(back);
        Assert.Equal(queryEncoding, back.QueryEncoding);
    }

    [Theory]
    [InlineData(TurboQuantBitSize.Bits1)]
    [InlineData(TurboQuantBitSize.Bits1Point5)]
    [InlineData(TurboQuantBitSize.Bits2)]
    [InlineData(TurboQuantBitSize.Bits4)]
    public void TurboQuantizationParams_Bits_Preserved(TurboQuantBitSize bits)
    {
        var original = new TurboQuantizationParams { Bits = bits, Memory = Memory.Cold };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.TurboQuantizationParams);
        TurboQuantizationParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.TurboQuantizationParams);
        Assert.NotNull(back);
        Assert.Equal(bits, back.Bits);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(Distance.Cosine)]
    [InlineData(Distance.Euclid)]
    [InlineData(Distance.Dot)]
    [InlineData(Distance.Manhattan)]
    public void Distance_AllVariants_Serialize(Distance original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Distance);
        Distance back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Distance);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(VectorStorageDatatype.Float32)]
    [InlineData(VectorStorageDatatype.Float16)]
    [InlineData(VectorStorageDatatype.Uint8)]
    [InlineData(VectorStorageDatatype.Turbo4)]
    public void VectorStorageDatatype_AllVariants_Serialize(VectorStorageDatatype original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.VectorStorageDatatype);
        VectorStorageDatatype back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.VectorStorageDatatype);
        Assert.Equal(original, back);
    }

    [Fact]
    public void MultiVectorConfig_MaxSim_RoundTrips()
    {
        var original = new MultiVectorConfig { Comparator = MultiVectorComparator.MaxSim };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.MultiVectorConfig);
        MultiVectorConfig? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.MultiVectorConfig);
        Assert.NotNull(back);
        Assert.Equal(original.Comparator, back.Comparator);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(Memory.Cold)]
    [InlineData(Memory.Cached)]
    [InlineData(Memory.Pinned)]
    public void Memory_AllVariants_Serialize(Memory original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Memory);
        Memory back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Memory);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ScalarType_Int8_Serializes()
    {
        const ScalarType original = ScalarType.Int8;
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ScalarType);
        ScalarType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ScalarType);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(CompressionRatio.X4)]
    [InlineData(CompressionRatio.X8)]
    [InlineData(CompressionRatio.X16)]
    [InlineData(CompressionRatio.X32)]
    [InlineData(CompressionRatio.X64)]
    public void CompressionRatio_AllVariants_Serialize(CompressionRatio original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.CompressionRatio);
        CompressionRatio back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.CompressionRatio);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeFloat_AllBounds_Preserved()
    {
        var original = new RangeFloat { Gte = 1.0, Gt = 2.0, Lte = 3.0, Lt = 4.0 };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeFloat);
        RangeFloat? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeFloat);
        Assert.NotNull(back);
        Assert.Equal(original.Gte, back.Gte);
        Assert.Equal(original.Gt, back.Gt);
        Assert.Equal(original.Lte, back.Lte);
        Assert.Equal(original.Lt, back.Lt);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeFloat_NullBounds_Preserved()
    {
        var original = new RangeFloat { };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeFloat);
        RangeFloat? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeFloat);
        Assert.NotNull(back);
        Assert.Null(back.Gte);
        Assert.Null(back.Gt);
        Assert.Null(back.Lte);
        Assert.Null(back.Lt);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeDatetime_AllBounds_Preserved()
    {
        var original = new RangeDatetime
        {
            Gte = "2024-01-01T00:00:00Z",
            Gt = "2024-02-01T00:00:00Z",
            Lte = "2024-03-01T00:00:00Z",
            Lt = "2024-04-01T00:00:00Z",
        };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeDatetime);
        RangeDatetime? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeDatetime);
        Assert.NotNull(back);
        Assert.Equal(original.Gte, back.Gte);
        Assert.Equal(original.Gt, back.Gt);
        Assert.Equal(original.Lte, back.Lte);
        Assert.Equal(original.Lt, back.Lt);
        Assert.Equal(original, back);
    }

    [Fact]
    public void RangeDatetime_NullBounds_Preserved()
    {
        var original = new RangeDatetime { };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.RangeDatetime);
        RangeDatetime? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.RangeDatetime);
        Assert.NotNull(back);
        Assert.Null(back.Gte);
        Assert.Null(back.Gt);
        Assert.Null(back.Lte);
        Assert.Null(back.Lt);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ValuesCount_AllBounds_Preserved()
    {
        var original = new ValuesCount { Gte = 1UL, Gt = 2UL, Lte = 3UL, Lt = 4UL };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ValuesCount);
        ValuesCount? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ValuesCount);
        Assert.NotNull(back);
        Assert.Equal(original.Gte, back.Gte);
        Assert.Equal(original.Gt, back.Gt);
        Assert.Equal(original.Lte, back.Lte);
        Assert.Equal(original.Lt, back.Lt);
        Assert.Equal(original, back);
    }

    [Fact]
    public void ValuesCount_NullBounds_Preserved()
    {
        var original = new ValuesCount { };
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.ValuesCount);
        ValuesCount? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.ValuesCount);
        Assert.NotNull(back);
        Assert.Null(back.Gte);
        Assert.Null(back.Gt);
        Assert.Null(back.Lte);
        Assert.Null(back.Lt);
        Assert.Equal(original, back);
    }

    [Theory]
    [InlineData(TokenizerType.Prefix)]
    [InlineData(TokenizerType.Whitespace)]
    [InlineData(TokenizerType.Word)]
    [InlineData(TokenizerType.Multilingual)]
    public void TokenizerType_AllVariants_Serialize(TokenizerType original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.TokenizerType);
        TokenizerType back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.TokenizerType);
        Assert.Equal(original, back);
    }

    public static IEnumerable<object[]> AllLanguages()
    {
        foreach (Language language in Enum.GetValues<Language>())
        {
            yield return new object[] { language };
        }
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Language_All30Values_Serialize(Language original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.Language);
        Language back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.Language);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Language_Has30Values()
    {
        Assert.Equal(30, Enum.GetValues<Language>().Length);
    }

    public static IEnumerable<object[]> AllSnowballLanguages()
    {
        foreach (SnowballLanguage language in Enum.GetValues<SnowballLanguage>())
        {
            yield return new object[] { language };
        }
    }

    [Theory]
    [MemberData(nameof(AllSnowballLanguages))]
    public void SnowballLanguage_All19Values_Serialize(SnowballLanguage original)
    {
        string json = JsonSerializer.Serialize(original, EdgeJsonContext.Deep.SnowballLanguage);
        SnowballLanguage back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.SnowballLanguage);
        Assert.Equal(original, back);
    }

    [Fact]
    public void SnowballLanguage_Has19Values()
    {
        Assert.Equal(19, Enum.GetValues<SnowballLanguage>().Length);
    }

    private static Stopwords RoundTripStopwords(Stopwords value)
    {
        PayloadIndexParams wrapper = PayloadIndexParams.Text(new TextIndexParams { Stopwords = value });
        string json = JsonSerializer.Serialize(wrapper, EdgeJsonContext.Deep.PayloadIndexParams);
        PayloadIndexParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexParams);
        var text = Assert.IsType<TextPayloadIndex>(back);
        Assert.NotNull(text.Config.Stopwords);
        return text.Config.Stopwords;
    }

    private static Stemmer RoundTripStemmer(Stemmer value)
    {
        PayloadIndexParams wrapper = PayloadIndexParams.Text(new TextIndexParams { Stemmer = value });
        string json = JsonSerializer.Serialize(wrapper, EdgeJsonContext.Deep.PayloadIndexParams);
        PayloadIndexParams? back = JsonSerializer.Deserialize(json, EdgeJsonContext.Deep.PayloadIndexParams);
        var text = Assert.IsType<TextPayloadIndex>(back);
        Assert.NotNull(text.Config.Stemmer);
        return text.Config.Stemmer;
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Stopwords_Language_Preserved(Language language)
    {
        Stopwords original = Stopwords.CreateLanguage(language);
        Stopwords back = RoundTripStopwords(original);
        var backLanguage = Assert.IsType<LanguageStopwords>(back);
        Assert.Equal(language, backLanguage.Language);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Stopwords_SetNulls_Preserved()
    {
        Stopwords original = Stopwords.Set(null, null);
        Stopwords back = RoundTripStopwords(original);
        var backSet = Assert.IsType<SetStopwords>(back);
        Assert.Null(backSet.Languages);
        Assert.Null(backSet.Custom);
    }

    [Fact]
    public void Stopwords_SetEmpty_Preserved()
    {
        Stopwords original = Stopwords.Set([], []);
        Stopwords back = RoundTripStopwords(original);
        var backSet = Assert.IsType<SetStopwords>(back);
        Assert.NotNull(backSet.Languages);
        Assert.NotNull(backSet.Custom);
        Assert.Empty(backSet.Languages);
        Assert.Empty(backSet.Custom);
    }

    [Fact]
    public void Stopwords_SetWithCustom_Preserved()
    {
        IReadOnlyList<Language> languages = [Language.English, Language.French];
        IReadOnlyList<string> custom = ["foo", "bar"];
        Stopwords original = Stopwords.Set(languages, custom);
        Stopwords back = RoundTripStopwords(original);
        var backSet = Assert.IsType<SetStopwords>(back);
        Assert.NotNull(backSet.Languages);
        Assert.NotNull(backSet.Custom);
        Assert.Equal(languages, backSet.Languages);
        Assert.Equal(custom, backSet.Custom);
    }

    [Theory]
    [MemberData(nameof(AllSnowballLanguages))]
    public void Stemmer_Snowball_Preserved(SnowballLanguage language)
    {
        Stemmer original = Stemmer.Snowball(language);
        Stemmer back = RoundTripStemmer(original);
        var backSnowball = Assert.IsType<SnowballStemmer>(back);
        Assert.Equal(language, backSnowball.Language);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Stemmer_Disabled_Preserved()
    {
        Stemmer original = Stemmer.Disabled();
        Stemmer back = RoundTripStemmer(original);
        Assert.IsType<DisabledStemmer>(back);
        Assert.Equal(original, back);
    }
}
