using System.Runtime.InteropServices;

namespace Qdrant.Edge;

/// <summary>Safe handle over an opaque native <c>QdrantEdgeShard*</c>.</summary>
internal sealed class EdgeShardHandle : SafeHandle
{
    /// <summary>Initializes an invalid handle.</summary>
    public EdgeShardHandle()
        : base(nint.Zero, ownsHandle: true)
    {
    }

    /// <summary>Initializes a handle over a live native pointer.</summary>
    /// <param name="handle">Native shard pointer.</param>
    public EdgeShardHandle(nint handle)
        : base(nint.Zero, ownsHandle: true)
    {
        SetHandle(handle);
    }

    /// <inheritdoc />
    public override bool IsInvalid => handle == nint.Zero;

    /// <inheritdoc />
    protected override bool ReleaseHandle()
    {
        try
        {
            return NativeMethods.qdrant_edge_shard_destroy(handle) == QdrantEdgeStatus.Ok;
        }
        catch (DllNotFoundException)
        {
            return true;
        }
    }
}
