namespace Qdrant.Edge;

internal sealed record VectorHnswRequest(string VectorName, HnswIndexConfig Hnsw);

internal sealed record SnapshotRequest(string Path, string? TmpDir);

internal sealed record UnpackRequest(string SnapshotPath, string TargetPath);
