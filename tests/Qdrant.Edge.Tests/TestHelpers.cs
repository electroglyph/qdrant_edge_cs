namespace Qdrant.Edge.Tests;

/// <summary>Shared fixtures mirroring the 4-dim Dot demo.</summary>
internal static class TestHelpers
{
    internal static EdgeConfig FourDimDotConfig() => new()
    {
        VectorData = new Dictionary<string, VectorDataConfig>
        {
            [""] = new VectorDataConfig { Size = 4, Distance = Distance.Dot },
        },
    };

    internal static Point FourDimPoint(ulong id, float a, float b, float c, float d) => new()
    {
        Id = PointId.FromNumber(id),
        Vector = Vector.Single([a, b, c, d]),
    };

    internal static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "qdrant-edge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
