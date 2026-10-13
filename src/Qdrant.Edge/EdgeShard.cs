using System.Runtime.InteropServices;

// JsonTypeInfo<T> lives in .Metadata on every supported target framework.
using JsonTypeInfoOfObject = System.Text.Json.Serialization.Metadata.JsonTypeInfo<object>;

namespace Qdrant.Edge;

/// <summary>
/// Read-write embedded shard (WAL + optimizers). Obtain via
/// <see cref="Create"/> or <see cref="Load"/>; release with
/// <see cref="Unload"/> or <see cref="Dispose"/>.
/// </summary>
public sealed class EdgeShard : IDisposable
{
    private readonly Lock _gate = new();
    private EdgeShardHandle? _handle;
    private bool _disposed;
    private readonly string _path;

    private EdgeShard(EdgeShardHandle handle, string path)
    {
        _handle = handle;
        _path = path;
    }

    /// <summary>Filesystem path the shard stores its data at.</summary>
    public string Path
    {
        get
        {
            lock (_gate)
            {
                ThrowIfUnloadedLocked();
                return _path;
            }
        }
    }

    /// <summary>Whether the shard is still loaded.</summary>
    public bool IsLoaded
    {
        get
        {
            lock (_gate)
            {
                return _handle is not null && !_handle.IsInvalid && !_disposed;
            }
        }
    }

    /// <summary>Creates a new shard at <paramref name="path"/>.</summary>
    /// <param name="path">Directory for shard data. Created when missing.</param>
    /// <param name="config">Shard configuration.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>The loaded shard.</returns>
    /// <exception cref="EdgeInvalidArgumentException">When config validation fails.</exception>
    /// <exception cref="EdgeOperationException">When the native library is missing or creation fails.</exception>
    public static EdgeShard Create(string path, EdgeConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        ArgumentNullException.ThrowIfNull(config, nameof(config));
        cancellationToken.ThrowIfCancellationRequested();
        config.Validate();
        return CreateCore(path, JsonSerializer.Serialize(config, EdgeJsonContext.Default.EdgeConfig), configJsonRequired: true);
    }

    /// <summary>Opens an existing shard at <paramref name="path"/>, or creates one when <paramref name="config"/> is given.</summary>
    /// <param name="path">Directory holding shard data.</param>
    /// <param name="config">Optional configuration for creation.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>The loaded shard.</returns>
    public static EdgeShard Load(string path, EdgeConfig? config = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        config?.Validate();
        string? configJson = config is null
            ? null
            : JsonSerializer.Serialize(config, EdgeJsonContext.Default.EdgeConfig);
        return LoadCore(path, configJson);
    }

    /// <summary>Flushes the WAL and segments to disk synchronously.</summary>
    /// <exception cref="ShardClosedException">When the shard is unloaded.</exception>
    public void Flush() => CallNative("flush", null, EdgeJsonContext.Default.Object);

    /// <summary>Runs segment optimizers to completion. Returns true when work was done.</summary>
    /// <returns>True when any segment was optimized.</returns>
    /// <exception cref="ShardClosedException">When the shard is unloaded.</exception>
    public bool Optimize()
    {
        string json = CallNativeRaw("optimize", null);
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.Boolean);
    }

    /// <summary>Returns the config the shard was loaded or created with.</summary>
    /// <returns>A snapshot of the shard config.</returns>
    /// <exception cref="ShardClosedException">When the shard is unloaded.</exception>
    public EdgeConfig GetConfig()
    {
        string json = CallNativeRaw("config", null);
        EdgeConfig? config = JsonSerializer.Deserialize(json, EdgeJsonContext.Default.EdgeConfig);
        return config ?? throw new EdgeOperationException("engine returned an empty config");
    }

    /// <summary>Sets the shard-global HNSW configuration and persists it.</summary>
    /// <param name="hnsw">New HNSW settings.</param>
    public void SetHnswConfig(HnswIndexConfig hnsw)
    {
        ArgumentNullException.ThrowIfNull(hnsw, nameof(hnsw));
        hnsw.Validate("hnsw config");
        CallNative("set_hnsw_config", JsonSerializer.Serialize(hnsw, EdgeJsonContext.Default.HnswIndexConfig), EdgeJsonContext.Default.Object);
    }

    /// <summary>Sets the HNSW configuration of one named vector field.</summary>
    /// <param name="vectorName">Vector field name.</param>
    /// <param name="hnsw">New HNSW settings.</param>
    public void SetVectorHnswConfig(string vectorName, HnswIndexConfig hnsw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorName, nameof(vectorName));
        ArgumentNullException.ThrowIfNull(hnsw, nameof(hnsw));
        hnsw.Validate($"vector field {vectorName}");
        CallNative(
            "set_vector_hnsw_config",
            JsonSerializer.Serialize(new VectorHnswRequest(vectorName, hnsw), EdgeJsonContext.Default.VectorHnswRequest),
            EdgeJsonContext.Default.Object);
    }

    /// <summary>Sets the optimizer configuration and persists it.</summary>
    /// <param name="optimizers">New optimizer settings.</param>
    public void SetOptimizersConfig(OptimizersConfig optimizers)
    {
        ArgumentNullException.ThrowIfNull(optimizers, nameof(optimizers));
        CallNative(
            "set_optimizers_config",
            JsonSerializer.Serialize(optimizers, EdgeJsonContext.Default.OptimizersConfig),
            EdgeJsonContext.Default.Object);
    }

    /// <summary>Returns the snapshot manifest as a JSON string.</summary>
    /// <returns>Manifest JSON.</returns>
    /// <exception cref="ShardClosedException">When the shard is unloaded.</exception>
    public string SnapshotManifest() => CallNativeRaw("snapshot_manifest", null);

    /// <summary>Replaces or patches this shard's data from a snapshot archive.</summary>
    /// <param name="snapshotPath">Snapshot archive path.</param>
    /// <param name="tmpDir">Temporary unpack directory; defaults to the snapshot parent.</param>
    /// <remarks>
    /// A full recovery replaces data entirely; a partial recovery merges in
    /// place and is not atomic. If a full recovery fails after the data
    /// directory was cleared, the shard is left unloaded.
    /// </remarks>
    public void UpdateFromSnapshot(string snapshotPath, string? tmpDir = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath, nameof(snapshotPath));
        if (tmpDir is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tmpDir, nameof(tmpDir));
        }

        CallNative(
            "update_from_snapshot",
            JsonSerializer.Serialize(
                new SnapshotRequest(snapshotPath, tmpDir), EdgeJsonContext.Default.SnapshotRequest),
            EdgeJsonContext.Default.Object);
    }

    /// <summary>Unpacks a snapshot archive into <paramref name="targetPath"/>.</summary>
    /// <param name="snapshotPath">Snapshot archive path.</param>
    /// <param name="targetPath">Destination directory.</param>
    public static void UnpackSnapshot(string snapshotPath, string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotPath, nameof(snapshotPath));
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath, nameof(targetPath));
        NativeLoader.EnsureLoaded();
        if (!NativeLoader.IsAvailable)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available; cannot unpack snapshot");
        }

        string request = JsonSerializer.Serialize(
            new UnpackRequest(snapshotPath, targetPath), EdgeJsonContext.Default.UnpackRequest);
        nint responsePtr = nint.Zero;
        QdrantEdgeStatus status;
        try
        {
            status = NativeMethods.qdrant_edge_call(nint.Zero, "unpack_snapshot", request, out responsePtr);
        }
        catch (DllNotFoundException ex)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available", ex);
        }

        FreeResponse(responsePtr);
        ThrowForStatus(status, "unpack snapshot");
    }

    /// <summary>
    /// Eagerly flushes and releases the shard. Idempotent; after success,
    /// operations fail with <see cref="ShardClosedException"/>.
    /// </summary>
    public void Unload()
    {
        lock (_gate)
        {
            if (_handle is null || _disposed)
            {
                return;
            }

            try
            {
                CallNativeLocked("flush", null);
            }
            catch (EdgeOperationException)
            {
                return;
            }

            _handle.Dispose();
            _handle = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Unload();
        GC.SuppressFinalize(this);
    }

    /// <summary>Applies an update operation to the shard.</summary>
    /// <param name="operation">Update to apply.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    public void Update(UpdateOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        cancellationToken.ThrowIfCancellationRequested();
        CallNative(
            "update",
            JsonSerializer.Serialize(operation, EdgeJsonContext.Default.UpdateOperation),
            EdgeJsonContext.Default.Object);
    }

    /// <summary>Runs a vector-only search. Deprecated in edge-core; prefer <see cref="Query"/>.</summary>
    /// <param name="request">Search request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Scored hits.</returns>
    public IReadOnlyList<ScoredPoint> Search(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "search",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.SearchRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.ScoredPointArray)
            ?? throw new EdgeOperationException("engine returned an empty search response");
    }

    /// <summary>Runs a query request.</summary>
    /// <param name="request">Query request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Scored hits.</returns>
    public IReadOnlyList<ScoredPoint> Query(QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "query",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.QueryRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.ScoredPointArray)
            ?? throw new EdgeOperationException("engine returned an empty query response");
    }

    /// <summary>Runs a batch of queries in one pass, preserving order. Cheaper than N x <see cref="Query"/>.</summary>
    /// <param name="request">Batch request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>One hit list per input query, in input order.</returns>
    public IReadOnlyList<IReadOnlyList<ScoredPoint>> QueryBatch(QueryBatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "query_batch",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.QueryBatchRequest));
        ScoredPoint[][]? batches = JsonSerializer.Deserialize(json, EdgeJsonContext.Default.ScoredPointArrayArray);
        return batches ?? throw new EdgeOperationException("engine returned an empty batch response");
    }

    /// <summary>Runs a grouped query.</summary>
    /// <param name="request">Group request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Groups.</returns>
    public IReadOnlyList<Group> QueryGroups(GroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "query_groups",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.GroupRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.GroupArray)
            ?? throw new EdgeOperationException("engine returned an empty groups response");
    }

    /// <summary>Scrolls points with pagination; follow <see cref="ScrollResponse.NextOffset"/> until null.</summary>
    /// <param name="request">Scroll request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Records plus the next offset.</returns>
    public ScrollResponse Scroll(ScrollRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "scroll",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.ScrollRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.ScrollResponse)
            ?? throw new EdgeOperationException("engine returned an empty scroll response");
    }

    /// <summary>Retrieves points by id; missing ids are omitted.</summary>
    /// <param name="request">Retrieve request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Found records.</returns>
    public IReadOnlyList<Record> Retrieve(RetrieveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "retrieve",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.RetrieveRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.RecordArray)
            ?? throw new EdgeOperationException("engine returned an empty retrieve response");
    }

    /// <summary>Counts points matching a filter.</summary>
    /// <param name="request">Count request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Point count.</returns>
    public ulong Count(CountRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "count",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.CountRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.UInt64);
    }

    /// <summary>Runs a facet aggregation over an indexed payload key.</summary>
    /// <param name="request">Facet request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Facet hits.</returns>
    public FacetResponse Facet(FacetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "facet",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.FacetRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.FacetResponse)
            ?? throw new EdgeOperationException("engine returned an empty facet response");
    }

    /// <summary>Returns summary information about the shard.</summary>
    /// <returns>Shard statistics.</returns>
    public ShardInfo GetInfo() => JsonSerializer.Deserialize(
        CallNativeRaw("info", null), EdgeJsonContext.Default.ShardInfo)
        ?? throw new EdgeOperationException("engine returned an empty info response");

    /// <summary>Runs an async query by offloading the sync engine call.</summary>
    /// <param name="request">Query request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch; cannot cancel in-flight native calls).</param>
    /// <returns>Scored hits.</returns>
    public Task<IReadOnlyList<ScoredPoint>> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Query(request, cancellationToken), cancellationToken);
    }

    /// <summary>Runs an async search by offloading the sync engine call.</summary>
    /// <param name="request">Search request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch; cannot cancel in-flight native calls).</param>
    /// <returns>Scored hits.</returns>
    public Task<IReadOnlyList<ScoredPoint>> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Search(request, cancellationToken), cancellationToken);
    }

#if QDRANT_EDGE_MATRIX
    /// <summary>Runs a search-matrix computation (experimental; O(n^2) cost).</summary>
    /// <param name="request">Matrix request.</param>
    /// <param name="cancellationToken">Cancellation token (checked before dispatch).</param>
    /// <returns>Sample ids and nearest lists.</returns>
    [System.Diagnostics.CodeAnalysis.Experimental("QDRANT_EDGE_MATRIX")]
    public SearchMatrixResponse SearchMatrix(SearchMatrixRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        string json = CallNativeRaw(
            "search_matrix",
            JsonSerializer.Serialize(request, EdgeJsonContext.Default.SearchMatrixRequest));
        return JsonSerializer.Deserialize(json, EdgeJsonContext.Default.SearchMatrixResponse)
            ?? throw new EdgeOperationException("engine returned an empty matrix response");
    }
#endif

    private static EdgeShard CreateCore(string path, string configJson, bool configJsonRequired)
    {
        NativeLoader.EnsureLoaded();
        if (!NativeLoader.IsAvailable)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available; build the cabi crate and ship runtimes/<rid>/native/ assets");
        }

        QdrantEdgeStatus status;
        nint handle = nint.Zero;
        try
        {
            status = NativeMethods.qdrant_edge_shard_create(path, configJson, out handle);
        }
        catch (DllNotFoundException ex)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available", ex);
        }

        if (status != QdrantEdgeStatus.Ok || handle == nint.Zero)
        {
            ThrowForStatus(status, "create shard");
        }

        return new EdgeShard(new EdgeShardHandle(handle), path);
    }

    private static EdgeShard LoadCore(string path, string? configJson)
    {
        NativeLoader.EnsureLoaded();
        if (!NativeLoader.IsAvailable)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available; build the cabi crate and ship runtimes/<rid>/native/ assets");
        }

        QdrantEdgeStatus status;
        nint handle = nint.Zero;
        try
        {
            status = NativeMethods.qdrant_edge_shard_load(path, configJson, out handle);
        }
        catch (DllNotFoundException ex)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available", ex);
        }

        if (status != QdrantEdgeStatus.Ok || handle == nint.Zero)
        {
            ThrowForStatus(status, "load shard");
        }

        return new EdgeShard(new EdgeShardHandle(handle), path);
    }

    private string CallNativeRaw(string method, string? requestJson)
    {
        lock (_gate)
        {
            return CallNativeLocked(method, requestJson);
        }
    }

    private void CallNative(string method, string? requestJson, JsonTypeInfoOfObject responseType)
    {
        string json = CallNativeRaw(method, requestJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        _ = JsonSerializer.Deserialize(json, responseType);
    }

    private string CallNativeLocked(string method, string? requestJson)
    {
        ThrowIfUnloadedLocked();
        nint responsePtr = nint.Zero;
        QdrantEdgeStatus status;
        nint handle = _handle is null ? nint.Zero : _handle.DangerousGetHandle();
        try
        {
            status = NativeMethods.qdrant_edge_call(handle, method, requestJson, out responsePtr);
        }
        catch (DllNotFoundException ex)
        {
            throw new EdgeOperationException("native qdrant_edge library is not available", ex);
        }

        string response = responsePtr == nint.Zero
            ? string.Empty
            : Marshal.PtrToStringUTF8(responsePtr) ?? string.Empty;
        FreeResponse(responsePtr);
        if (status != QdrantEdgeStatus.Ok)
        {
            ThrowForStatus(status, method);
        }

        return response;
    }

    private void ThrowIfUnloadedLocked()
    {
        if (_disposed || _handle is null || _handle.IsInvalid)
        {
            throw new ShardClosedException();
        }
    }

    private static void ThrowForStatus(QdrantEdgeStatus status, string method)
    {
        string reason = ReadLastError();
        throw status switch
        {
            QdrantEdgeStatus.ShardClosed => new ShardClosedException(),
            QdrantEdgeStatus.InvalidArgument => new EdgeInvalidArgumentException($"{method}: {reason}"),
            _ => new EdgeOperationException($"{method}: {reason}"),
        };
    }

    private static string ReadLastError()
    {
        try
        {
            nint ptr = NativeMethods.qdrant_edge_last_error();
            return ptr == nint.Zero ? "unknown error" : Marshal.PtrToStringUTF8(ptr) ?? "unknown error";
        }
        catch (DllNotFoundException)
        {
            return "native library is not available";
        }
    }

    private static void FreeResponse(nint responsePtr)
    {
        if (responsePtr == nint.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.qdrant_edge_free_string(responsePtr);
        }
        catch (DllNotFoundException)
        {
        }
    }
}
