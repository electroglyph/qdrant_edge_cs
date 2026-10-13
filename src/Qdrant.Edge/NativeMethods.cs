using System.Runtime.InteropServices;

namespace Qdrant.Edge;

/// <summary>Native status codes returned by the stable C ABI crate.</summary>
internal enum QdrantEdgeStatus : int
{
    /// <summary>Success.</summary>
    Ok = 0,

    /// <summary>Invalid argument.</summary>
    InvalidArgument = 1,

    /// <summary>Engine operational failure.</summary>
    OperationError = 2,

    /// <summary>Shard was unloaded.</summary>
    ShardClosed = 3,
}

/// <summary>
/// Source-generated P/Invoke surface for the <c>qdrant_edge_*</c> C ABI.
/// All requests/responses cross the boundary as UTF-8 JSON strings.
/// </summary>
internal static partial class NativeMethods
{
    internal const string LibraryName = "qdrant_edge";

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial QdrantEdgeStatus qdrant_edge_shard_create(
        string path,
        string configJson,
        out nint handle);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial QdrantEdgeStatus qdrant_edge_shard_load(
        string path,
        string? configJson,
        out nint handle);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial QdrantEdgeStatus qdrant_edge_shard_destroy(nint handle);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial QdrantEdgeStatus qdrant_edge_call(
        nint handle,
        string method,
        string? requestJson,
        out nint responseJson);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint qdrant_edge_last_error();

    [LibraryImport(LibraryName)]
    internal static partial void qdrant_edge_free_string(nint text);
}
