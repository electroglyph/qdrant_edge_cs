pub(crate) mod config;
pub(crate) mod error;
pub(crate) mod filter;
pub(crate) mod formula;
pub(crate) mod handles;
pub(crate) mod json;
pub(crate) mod ops;
pub(crate) mod query;
pub(crate) mod types;
pub(crate) mod update;

pub(crate) use error::{ApiError, Result};
use error::{STATUS_OK, STATUS_OPERATION_ERROR};

use std::cell::RefCell;
use std::ffi::{CStr, CString, c_char};
use std::panic::AssertUnwindSafe;
use std::path::Path;

use serde_json::Value;

thread_local! {
    static LAST_ERROR: RefCell<CString> =
        RefCell::new(CString::new("").expect("empty string contains no nul byte"));
}

/// Record the detail behind a non-zero status. The C# side copies the string
/// immediately and never frees it, so thread-local storage (never null, valid
/// until the next call on this thread) is the right lifetime.
pub(crate) fn set_last_error(msg: &str) {
    let owned =
        CString::new(msg).unwrap_or_else(|_| CString::new(msg.replace('\0', "")).unwrap_or_default());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = owned);
}

/// Detail behind the last non-zero status on this thread. Never null; empty
/// string when the last call succeeded.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_last_error() -> *const c_char {
    LAST_ERROR.with(|slot| slot.borrow().as_ptr())
}

/// Release a response string previously returned through `out_response`.
/// Null-safe: C# passes null when the call produced no response.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_free_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    unsafe {
        drop(CString::from_raw(ptr));
    }
}

/// Store `s` in `out`, transferring ownership to the caller. A null `out`
/// pointer means the caller passed no storage; ignore it.
fn out_string(out: *mut *mut c_char, s: &str) {
    if out.is_null() {
        return;
    }
    let owned =
        CString::new(s).unwrap_or_else(|_| CString::new(s.replace('\0', "")).unwrap_or_default());
    unsafe {
        *out = owned.into_raw();
    }
}

/// Borrow a C# UTF-8 string argument. Null and non-UTF-8 pointers are caller
/// bugs, reported as `InvalidArgument`.
fn read_c_str<'a>(ptr: *const c_char) -> Result<&'a str> {
    if ptr.is_null() {
        return Err(ApiError::invalid_argument("string argument is null"));
    }
    unsafe {
        CStr::from_ptr(ptr)
            .to_str()
            .map_err(|_| ApiError::invalid_argument("string argument is not valid UTF-8"))
    }
}

/// Run `run` under `catch_unwind` and translate the outcome to a status code:
/// a panic becomes an `OperationError` (a panic must never unwind into C#),
/// an `Err` is stashed for `qdrant_edge_last_error`, and an empty `Ok`
/// response (used by calls with no meaningful output) yields a null pointer
/// instead of an allocated empty string.
fn invoke(out_response: *mut *mut c_char, run: impl FnOnce() -> Result<String>) -> i32 {
    let outcome = std::panic::catch_unwind(AssertUnwindSafe(run));
    let result = match outcome {
        Ok(inner) => inner,
        Err(_) => Err(ApiError {
            status: STATUS_OPERATION_ERROR,
            message: "internal engine panic".to_owned(),
        }),
    };
    match result {
        Ok(response) => {
            if !response.is_empty() {
                out_string(out_response, &response);
            }
            STATUS_OK
        }
        Err(err) => {
            set_last_error(&err.message);
            err.status
        }
    }
}

/// Create a new shard at `path`, failing when segments already exist.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_shard_create(
    path: *const c_char,
    config_json: *const c_char,
    out_handle: *mut isize,
) -> i32 {
    let run = || run_create(path, config_json, out_handle);
    invoke(std::ptr::null_mut(), run)
}

/// Open the shard at `path`, creating it when `config_json` is given. A null
/// `config_json` means no config override.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_shard_load(
    path: *const c_char,
    config_json: *const c_char,
    out_handle: *mut isize,
) -> i32 {
    let run = || run_load(path, config_json, out_handle);
    invoke(std::ptr::null_mut(), run)
}

/// Destroy the shard behind `handle`, releasing its WAL and segment files. An
/// unknown handle means the shard is already gone.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_shard_destroy(handle: isize) -> i32 {
    let run = || -> Result<String> {
        if handles::remove(handle) {
            Ok(String::new())
        } else {
            Err(ApiError::shard_closed())
        }
    };
    invoke(std::ptr::null_mut(), run)
}

/// Dispatch a JSON-in/JSON-out `method` call. A null `request_json` means no
/// request body. `unpack_snapshot` is process-wide and takes no valid handle;
/// every other method requires one.
#[unsafe(no_mangle)]
pub extern "C" fn qdrant_edge_call(
    handle: isize,
    method: *const c_char,
    request_json: *const c_char,
    out_response: *mut *mut c_char,
) -> i32 {
    let run = || -> Result<String> {
        let method = read_c_str(method)?;
        let request = if request_json.is_null() {
            None
        } else {
            Some(read_c_str(request_json)?)
        };
        ops::call(method, handle, request)
    };
    invoke(out_response, run)
}

fn run_create(
    path: *const c_char,
    config_json: *const c_char,
    out_handle: *mut isize,
) -> Result<String> {
    let path = read_c_str(path)?;
    let cfg = parse_config(read_c_str(config_json)?)?;
    let shard = edge::EdgeShard::new(Path::new(path), cfg).map_err(ApiError::from)?;
    store_handle(out_handle, shard)
}

fn run_load(
    path: *const c_char,
    config_json: *const c_char,
    out_handle: *mut isize,
) -> Result<String> {
    let path = read_c_str(path)?;
    let cfg = if config_json.is_null() {
        None
    } else {
        Some(parse_config(read_c_str(config_json)?)?)
    };
    let shard = edge::EdgeShard::load(Path::new(path), cfg).map_err(ApiError::from)?;
    store_handle(out_handle, shard)
}

/// Parse a C# `EdgeConfig` JSON document into the engine config. Validation
/// lives here so an invalid config fails before the engine ever sees it.
fn parse_config(raw: &str) -> Result<edge::EdgeConfig> {
    let value: Value = serde_json::from_str(raw)
        .map_err(|err| ApiError::invalid_argument(format!("invalid config JSON: {err}")))?;
    config::parse_edge_config(&value)
}

fn store_handle(out_handle: *mut isize, shard: edge::EdgeShard) -> Result<String> {
    let handle = handles::insert(shard);
    if !out_handle.is_null() {
        unsafe {
            *out_handle = handle;
        }
    }
    Ok(String::new())
}
