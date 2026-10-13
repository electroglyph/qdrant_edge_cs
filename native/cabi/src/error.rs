use segment::common::operation_error::OperationError;

/// Status codes returned by the `qdrant_edge_*` C functions. Must match the
/// C# `QdrantEdgeStatus` enum (`Ok = 0`, `InvalidArgument = 1`,
/// `OperationError = 2`, `ShardClosed = 3`).
pub(crate) const STATUS_OK: i32 = 0;
pub(crate) const STATUS_INVALID_ARGUMENT: i32 = 1;
pub(crate) const STATUS_OPERATION_ERROR: i32 = 2;
pub(crate) const STATUS_SHARD_CLOSED: i32 = 3;

/// Error returned from fallible C ABI operations. The `status` travels as the
/// function's integer return value; the `message` is stashed in thread-local
/// storage for `qdrant_edge_last_error`.
#[derive(Debug)]
pub(crate) struct ApiError {
    pub(crate) status: i32,
    pub(crate) message: String,
}

impl ApiError {
    /// Host-supplied input was invalid; fix it and retry.
    pub(crate) fn invalid_argument(msg: impl Into<String>) -> Self {
        Self {
            status: STATUS_INVALID_ARGUMENT,
            message: msg.into(),
        }
    }

    /// Any other engine failure (I/O, missing index, dimension mismatch, …).
    pub(crate) fn operation_error(msg: impl Into<String>) -> Self {
        Self {
            status: STATUS_OPERATION_ERROR,
            message: msg.into(),
        }
    }

    /// The handle refers to a destroyed shard; open it again to continue.
    pub(crate) fn shard_closed() -> Self {
        Self {
            status: STATUS_SHARD_CLOSED,
            message: "shard is closed".to_owned(),
        }
    }
}

impl From<OperationError> for ApiError {
    fn from(err: OperationError) -> Self {
        ApiError::operation_error(err.to_string())
    }
}

pub(crate) type Result<T> = std::result::Result<T, ApiError>;

/// Clamp a host-supplied u64 count to usize, saturating instead of truncating.
/// On 64-bit this is a no-op; on 32-bit it avoids silent wrap. Use this for
/// tuning knobs where a huge value is merely slow (e.g. `hnsw_ef`,
/// `full_scan_threshold`), NOT for counts that drive an eager allocation —
/// those must go through [`bounded_limit`].
pub(crate) fn clamp_usize(v: u64) -> usize {
    usize::try_from(v).unwrap_or(usize::MAX)
}

/// Hard upper bound for host-supplied result counts (`limit`/`offset`).
///
/// An unbounded `limit` flows into eager allocations in the engine (e.g.
/// `HashSet::with_capacity(limit)` on the random-scroll path), so a value like
/// `u64::MAX` would request a multi-terabyte allocation and abort the host
/// process — an abort that `panic = "unwind"` cannot catch. On a single-user
/// on-device database a request for more than a million results is a bug or an
/// attack, so we reject it as a catchable error rather than let it allocate.
pub(crate) const MAX_RESULT_COUNT: u64 = 1_048_576; // 1 Mi

/// Validate and convert a host-supplied result count (`limit`/`offset`) to
/// `usize`, rejecting values above [`MAX_RESULT_COUNT`] with `InvalidArgument`
/// instead of letting them drive an unbounded allocation.
pub(crate) fn bounded_limit(field: &str, v: u64) -> Result<usize> {
    if v > MAX_RESULT_COUNT {
        return Err(ApiError::invalid_argument(format!(
            "{field} ({v}) exceeds the maximum of {MAX_RESULT_COUNT}"
        )));
    }
    // v <= MAX_RESULT_COUNT <= usize::MAX on every supported target.
    Ok(v as usize)
}

/// Maximum nesting depth for host-supplied filter and prefetch trees.
///
/// `Condition::Filter` and nested `Prefetch` are self-recursive, and their
/// conversions recurse one Rust stack frame per level. An unbounded
/// host-supplied tree (e.g. built from user-derived saved-search data) would
/// overflow the stack — a `SIGABRT` that `panic = "unwind"` cannot catch. A
/// legitimate filter/prefetch nests only a handful of levels, so we reject
/// anything deeper as a catchable `InvalidArgument`. Well below the frame
/// budget of an 8 MiB stack.
pub(crate) const MAX_QUERY_NESTING_DEPTH: u32 = 64;

/// Reject a filter/prefetch tree nested beyond [`MAX_QUERY_NESTING_DEPTH`]
/// before it can recurse deep enough to overflow the stack.
pub(crate) fn check_nesting_depth(kind: &str, depth: u32) -> Result<()> {
    if depth > MAX_QUERY_NESTING_DEPTH {
        return Err(ApiError::invalid_argument(format!(
            "{kind} nesting exceeds the maximum depth of {MAX_QUERY_NESTING_DEPTH}"
        )));
    }
    Ok(())
}

/// Maximum total node count for a host-supplied formula expression tree.
///
/// Depth alone ([`check_nesting_depth`]) does not bound an expression: each
/// combinator eagerly deep-clones its children's inner tree, and the host
/// holds the node handles, so reusing one handle as several children grows the
/// node count exponentially while depth stays under the cap — an eager
/// multi-gigabyte clone that aborts the process (uncatchable under
/// `panic = "unwind"`). A legitimate scoring formula has a handful of nodes,
/// so we reject anything in the thousands. Node count is tracked with
/// saturating adds so the counter itself cannot wrap.
pub(crate) const MAX_FORMULA_NODES: u64 = 10_000;

/// Reject a formula expression tree with more than [`MAX_FORMULA_NODES`] nodes
/// before the eager clone can exhaust memory. Complements
/// [`check_nesting_depth`], which bounds height (stack safety) but not the node
/// count (heap safety) — both guards are required.
pub(crate) fn check_expression_size(size: u64) -> Result<()> {
    if size > MAX_FORMULA_NODES {
        return Err(ApiError::invalid_argument(format!(
            "formula expression size ({size}) exceeds the maximum of {MAX_FORMULA_NODES} nodes"
        )));
    }
    Ok(())
}
