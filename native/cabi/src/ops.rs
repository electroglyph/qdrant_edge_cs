use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use edge::EdgeShardRead as _;
use serde_json::Value;

use crate::error::{ApiError, Result};
use crate::handles;
use crate::json::{as_str, opt_str, required};

/// Dispatch a JSON-in/JSON-out method call.
///
/// `unpack_snapshot` is process-wide and never touches the handle registry.
/// `update_from_snapshot` checks the handle and parses its request through
/// [`handles::with`] exactly once, then runs the snapshot recovery — which
/// needs ownership of the shard — outside the registry lock: the registry
/// mutex is not reentrant, so no helper below calls `with` again.
/// Every other method resolves its shard through `with` exactly once and runs
/// entirely inside the closure.
pub(crate) fn call(method: &str, handle: isize, request_json: Option<&str>) -> Result<String> {
    match method {
        "unpack_snapshot" => unpack_snapshot(request_json),
        "update_from_snapshot" => update_from_snapshot(handle, request_json),
        _ => handles::with(handle, |shard| dispatch(method, shard, request_json)),
    }
}

/// Handle-bound methods running on `&edge::EdgeShard`. Each arm parses its
/// request, calls the engine, and builds the response string; empty responses
/// mark calls with no meaningful output.
fn dispatch(method: &str, shard: &edge::EdgeShard, request_json: Option<&str>) -> Result<String> {
    match method {
        "flush" => {
            shard.flush().map_err(ApiError::from)?;
            Ok(String::new())
        }
        "optimize" => {
            let optimized = shard.optimize().map_err(ApiError::from)?;
            Ok(optimized.to_string())
        }
        "config" => Ok(json_text(super::config::edge_config_to_json(
            &shard.config(),
        ))?),
        "set_hnsw_config" => {
            let request = body(request_json, method)?;
            let hnsw = super::config::parse_hnsw_config(&request)?;
            shard.set_hnsw_config(hnsw).map_err(ApiError::from)?;
            Ok(String::new())
        }
        "set_vector_hnsw_config" => {
            let request = body(request_json, method)?;
            let ctx = "vector hnsw request";
            let name = as_str(required(&request, "VectorName", ctx)?, ctx, "VectorName")?;
            let hnsw = super::config::parse_hnsw_config(required(&request, "Hnsw", ctx)?)?;
            shard
                .set_vector_hnsw_config(name, hnsw)
                .map_err(ApiError::from)?;
            Ok(String::new())
        }
        "set_optimizers_config" => {
            let request = body(request_json, method)?;
            let optimizers = super::config::parse_optimizers_config(&request)?;
            shard
                .set_optimizers_config(optimizers)
                .map_err(ApiError::from)?;
            Ok(String::new())
        }
        "snapshot_manifest" => {
            let manifest = shard.snapshot_manifest().map_err(ApiError::from)?;
            serde_json::to_string(&manifest).map_err(|err| {
                ApiError::operation_error(format!(
                    "failed to serialize snapshot manifest: {err}"
                ))
            })
        }
        "update" => {
            let request = body(request_json, method)?;
            let operation = super::update::parse_update_operation(&request)?;
            shard.update(operation).map_err(ApiError::from)?;
            Ok(String::new())
        }
        "search" => {
            let request = body(request_json, method)?;
            let search = super::query::parse_search_request(&request)?;
            let hits = shard.search(search).map_err(ApiError::from)?;
            Ok(json_text(super::query::scored_list_to_json(hits))?)
        }
        "query" => {
            let request = body(request_json, method)?;
            let query = super::query::parse_query_request(&request)?;
            let hits = shard.query(query).map_err(ApiError::from)?;
            Ok(json_text(super::query::scored_list_to_json(hits))?)
        }
        "query_batch" => {
            let request = body(request_json, method)?;
            let batch = super::query::parse_query_batch_request(&request)?;
            let batches = shard.query_batch(batch).map_err(ApiError::from)?;
            Ok(json_text(super::query::batch_to_json(batches))?)
        }
        "query_groups" => {
            let request = body(request_json, method)?;
            let groups = super::query::parse_group_request(&request)?;
            let groups = shard.query_groups(groups).map_err(ApiError::from)?;
            Ok(json_text(super::query::groups_to_json(groups))?)
        }
        "scroll" => {
            let request = body(request_json, method)?;
            let scroll = super::query::parse_scroll_request(&request)?;
            let (records, next) = shard.scroll(scroll).map_err(ApiError::from)?;
            Ok(json_text(super::query::scroll_to_json(records, next))?)
        }
        "retrieve" => {
            let request = body(request_json, method)?;
            let retrieve = super::query::parse_retrieve_request(&request)?;
            let records = shard.retrieve(retrieve).map_err(ApiError::from)?;
            Ok(json_text(super::query::retrieve_to_json(records))?)
        }
        "count" => {
            let request = body(request_json, method)?;
            let count = super::query::parse_count_request(&request)?;
            let total = shard.count(count).map_err(ApiError::from)?;
            Ok(total.to_string())
        }
        "facet" => {
            let request = body(request_json, method)?;
            let facet = super::query::parse_facet_request(&request)?;
            let response = shard.facet(facet).map_err(ApiError::from)?;
            Ok(json_text(super::query::facet_to_json(response))?)
        }
        "info" => {
            let info = shard.info().map_err(ApiError::from)?;
            Ok(json_text(super::query::shard_info_to_json(info))?)
        }
        "search_matrix" => {
            let request = body(request_json, method)?;
            let matrix = super::query::parse_matrix_request(&request)?;
            let response = shard.search_matrix(matrix).map_err(ApiError::from)?;
            Ok(json_text(super::query::matrix_to_json(response))?)
        }
        unknown => Err(ApiError::invalid_argument(format!(
            "unknown method {unknown:?}"
        ))),
    }
}

/// Render a response document to the string crossing the C boundary.
/// Serialization of an assembled `Value` cannot fail; a failure here would be
/// an engine bug rather than host input, so it travels as `OperationError`.
fn json_text(value: Value) -> Result<String> {
    serde_json::to_string(&value)
        .map_err(|err| ApiError::operation_error(format!("failed to serialize response: {err}")))
}

/// Parse the request body of a method that requires one. A missing body and a
/// malformed document are both host input errors.
fn body(request_json: Option<&str>, method: &str) -> Result<Value> {
    match request_json {
        None => Err(ApiError::invalid_argument(format!(
            "{method} requires a request body"
        ))),
        Some(raw) => serde_json::from_str(raw).map_err(|err| {
            ApiError::invalid_argument(format!("{method} has an invalid request body: {err}"))
        }),
    }
}

/// Unpack a snapshot archive into a directory. Process-wide: takes no handle
/// and never touches the registry.
fn unpack_snapshot(request_json: Option<&str>) -> Result<String> {
    let request = body(request_json, "unpack_snapshot")?;
    let ctx = "unpack request";
    let snapshot = as_str(required(&request, "SnapshotPath", ctx)?, ctx, "SnapshotPath")?;
    let target = as_str(required(&request, "TargetPath", ctx)?, ctx, "TargetPath")?;
    edge::EdgeShard::unpack_snapshot(Path::new(snapshot), Path::new(target))
        .map_err(ApiError::from)?;
    Ok(String::new())
}

/// Paths and pre-recovery state collected for a snapshot recovery.
struct SnapshotSelection {
    snapshot: PathBuf,
    tmp: PathBuf,
    shard_path: PathBuf,
    current: edge::internal::SnapshotManifest,
}

/// Replace or patch the shard's data from a snapshot archive, mirroring the
/// file-level recovery: unpack and validate before touching the shard, then a
/// full snapshot replaces the data while a partial one merges into it.
///
/// Recovery needs ownership of the shard, which the shared registry handle
/// cannot grant through `with` (it yields only `&`), so the recovered shard
/// is swapped back under the same id. The caller's handle stays valid; only
/// a handle destroyed mid-recovery closes, matching a failed recovery.
fn update_from_snapshot(handle: isize, request_json: Option<&str>) -> Result<String> {
    let selection = handles::with(handle, |shard| {
        let request = body(request_json, "update_from_snapshot")?;
        let ctx = "snapshot request";
        let snapshot = PathBuf::from(as_str(required(&request, "Path", ctx)?, ctx, "Path")?);
        let tmp = match opt_str(&request, ctx, "TmpDir")? {
            Some(dir) => PathBuf::from(dir),
            None => snapshot
                .parent()
                .map(Path::to_path_buf)
                .unwrap_or_else(std::env::temp_dir),
        };
        Ok(SnapshotSelection {
            snapshot,
            tmp,
            shard_path: shard.path().to_path_buf(),
            current: shard.snapshot_manifest().map_err(ApiError::from)?,
        })
    })?;

    let unpack_dir = create_unpack_dir(&selection.tmp)?;
    let outcome = recover_snapshot(handle, &selection, &unpack_dir);
    let _ = std::fs::remove_dir_all(&unpack_dir);
    outcome
}

/// Run the snapshot file surgery after the archive is unpacked and validated.
/// The recovered shard is swapped back under the same handle id, so existing
/// callers keep working; a handle destroyed mid-recovery closes instead.
fn recover_snapshot(
    handle: isize,
    selection: &SnapshotSelection,
    unpack_dir: &Path,
) -> Result<String> {
    let SnapshotSelection {
        snapshot,
        shard_path,
        current,
        ..
    } = selection;
    edge::EdgeShard::unpack_snapshot(snapshot, unpack_dir).map_err(ApiError::from)?;
    let snapshot_manifest =
        edge::internal::SnapshotManifest::load_from_snapshot(unpack_dir, None)
            .map_err(ApiError::from)?;
    let fresh = if snapshot_manifest.is_empty() {        shard::files::clear_data(shard_path).map_err(|err| {
            ApiError::operation_error(format!("failed to clear shard data: {err}"))
        })?;
        shard::files::move_data(unpack_dir, shard_path).map_err(|err| {
            ApiError::operation_error(format!("failed to move snapshot data into place: {err}"))
        })?;
        edge::EdgeShard::load(shard_path, None).map_err(ApiError::from)?
    } else {
        edge::EdgeShard::recover_partial_snapshot(
            shard_path,
            current,
            unpack_dir,
            &snapshot_manifest,
        )
        .map_err(ApiError::from)?
    };
    if handles::replace(handle, fresh) {
        Ok(String::new())
    } else {
        Err(ApiError::shard_closed())
    }
}

static UNPACK_COUNTER: AtomicU64 = AtomicU64::new(0);

/// Create a fresh unpack directory under `tmp`, without a tempfile
/// dependency. Collisions retry with a new counter value.
fn create_unpack_dir(tmp: &Path) -> Result<PathBuf> {
    for _ in 0..100 {
        let dir = tmp.join(format!(
            "qdrant-edge-unpack-{}-{}",
            std::process::id(),
            UNPACK_COUNTER.fetch_add(1, Ordering::Relaxed),
        ));
        match std::fs::create_dir(&dir) {
            Ok(()) => return Ok(dir),
            Err(err) if err.kind() == std::io::ErrorKind::AlreadyExists => continue,
            Err(err) => {
                return Err(ApiError::operation_error(format!(
                    "failed to create snapshot unpack dir: {err}"
                )));
            }
        }
    }
    Err(ApiError::operation_error(
        "failed to create snapshot unpack dir: too many collisions",
    ))
}
