namespace Qdrant.Edge;

/// <summary>
/// Thrown when an operation targets a shard that was released via
/// <see cref="EdgeShard.Unload"/> or <see cref="EdgeShard.Dispose"/>.
/// </summary>
public sealed class ShardClosedException : EdgeException
{
    /// <summary>Initializes a new instance.</summary>
    public ShardClosedException()
        : base("shard is closed")
    {
    }
}
