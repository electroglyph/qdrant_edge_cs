using System.Text.Json.Nodes;

namespace Qdrant.Edge.Tests;

/// <summary>Text-index stopword parity pins; no-op when the native engine is absent.</summary>
public sealed class EdgeShardTextIndexNativeTests
{
    [Fact]
    public void TextIndex_StopwordTitle_FiltersWithStopwords()
    {
        if (!NativeLoader.IsAvailable)
        {
            return;
        }

        using EdgeShard shard = Seed(out _);
        Assert.Equal(1ul, shard.Count(new CountRequest { Filter = TitleIs("point two") }));
        Assert.Equal(0ul, shard.Count(new CountRequest { Filter = TitleIs("point") }));
    }

    private static Filter TitleIs(string text) => new()
    {
        Must = [Condition.Field(new FieldCondition { Key = "title", Match = Match.CreateText(text) })],
    };

    private static EdgeShard Seed(out string dir)
    {
        dir = TestHelpers.TempDir();
        EdgeShard shard = EdgeShard.Create(dir, TestHelpers.FourDimDotConfig());
        try
        {
            shard.Update(UpdateOperation.CreateFieldIndexWithParams(
                "title",
                PayloadIndexParams.Text(new TextIndexParams
                {
                    Stopwords = Stopwords.Set(null, ["point"]),
                })));
            shard.Update(UpdateOperation.UpsertPoints(
            [
                TestHelpers.FourDimPoint(1, 1f, 2f, 3f, 4f) with
                {
                    Payload = new JsonObject { ["title"] = "point one" },
                },
                TestHelpers.FourDimPoint(2, 2f, 3f, 4f, 5f) with
                {
                    Payload = new JsonObject { ["title"] = "point two" },
                },
            ]));
            return shard;
        }
        catch
        {
            shard.Dispose();
            throw;
        }
    }
}
