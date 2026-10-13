use std::collections::HashMap;
use std::sync::{LazyLock, Mutex, MutexGuard};
use std::sync::atomic::{AtomicIsize, Ordering};

use std::sync::Arc;

use crate::error::{ApiError, Result};

/// Process-wide handle table mapping the integers handed to C# back to live
/// shards. `edge::EdgeShard` operations take `&self` and synchronize
/// internally, so sharing one entry across host threads is safe; the mutex
/// guards only the table itself, not the operations.
static REGISTRY: LazyLock<Mutex<HashMap<isize, Arc<edge::EdgeShard>>>> =
    LazyLock::new(|| Mutex::new(HashMap::new()));
static NEXT_ID: AtomicIsize = AtomicIsize::new(1);

/// Lock the table, tolerating poisoning: a poisoned mutex still owns valid
/// entries, and refusing service after one failed operation would wedge every
/// shard for the rest of the process.
fn lock_registry() -> MutexGuard<'static, HashMap<isize, Arc<edge::EdgeShard>>> {
    REGISTRY.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
}

pub(crate) fn insert(shard: edge::EdgeShard) -> isize {
    // `fetch_add` returns a unique value per call, so Relaxed is enough: no
    // ordering with other memory accesses is required for ID allocation.
    let handle = NEXT_ID.fetch_add(1, Ordering::Relaxed);
    lock_registry().insert(handle, Arc::new(shard));
    handle
}

pub(crate) fn remove(handle: isize) -> bool {
    lock_registry().remove(&handle).is_some()
}

/// Swap the shard behind `handle`, keeping the id alive across snapshot
/// recovery. Returns false when the handle is unknown, in which case the
/// replacement is dropped by the caller.
pub(crate) fn replace(handle: isize, shard: edge::EdgeShard) -> bool {
    let mut registry = lock_registry();
    if registry.contains_key(&handle) {
        registry.insert(handle, Arc::new(shard));
        true
    } else {
        false
    }
}

/// Run `f` over the shard behind `handle`, failing with `ShardClosed` when the
/// handle is unknown. Every handle-bound `qdrant_edge_call` method goes
/// through here, so the closed-check takes precedence over any request
/// parsing error inside `f`.
///
/// The table lock is released before `f` runs: the shard is held through a
/// cloned `Arc`, so engine work (I/O, search, optimization) never serializes
/// unrelated shards behind the global mutex.
pub(crate) fn with<R>(handle: isize, f: impl FnOnce(&edge::EdgeShard) -> Result<R>) -> Result<R> {
    let shard = {
        let registry = lock_registry();
        registry
            .get(&handle)
            .cloned()
            .ok_or_else(ApiError::shard_closed)?
    };
    f(&shard)
}
