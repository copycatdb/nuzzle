#![allow(private_interfaces, clippy::not_unsafe_ptr_arg_deref)]

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::sync::Mutex;

use tokio::net::TcpStream;
use tokio::runtime::Runtime;
use tokio_util::compat::TokioAsyncWriteCompatExt;

use tabby::connection::Config;
use tabby::row_writer::RowWriter;
use tabby::{Client as TdsClient, Column};

// ── Thread-local last error ────────────────────────────────────────

thread_local! {
    static LAST_ERROR: std::cell::RefCell<Option<CString>> = const { std::cell::RefCell::new(None) };
}

fn set_error(msg: &str) {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = CString::new(msg).ok();
    });
}

#[no_mangle]
pub extern "C" fn nuzzle_last_error() -> *const c_char {
    LAST_ERROR.with(|e| {
        e.borrow()
            .as_ref()
            .map(|s| s.as_ptr())
            .unwrap_or(std::ptr::null())
    })
}

// ── Value types ────────────────────────────────────────────────────

/// Type tags for values returned by nuzzle_result_get_value
pub const NUZZLE_TYPE_NULL: i32 = 0;
pub const NUZZLE_TYPE_BOOL: i32 = 1;
pub const NUZZLE_TYPE_I64: i32 = 2;
pub const NUZZLE_TYPE_F64: i32 = 3;
pub const NUZZLE_TYPE_STRING: i32 = 4;
pub const NUZZLE_TYPE_BYTES: i32 = 5;

#[repr(C)]
pub struct NuzzleValue {
    pub type_tag: i32,
    pub int_val: i64,
    pub float_val: f64,
    pub str_val: *const c_char,
    pub str_len: i32,
    pub bytes_val: *const u8,
    pub bytes_len: i32,
}

impl Default for NuzzleValue {
    fn default() -> Self {
        Self {
            type_tag: NUZZLE_TYPE_NULL,
            int_val: 0,
            float_val: 0.0,
            str_val: std::ptr::null(),
            str_len: 0,
            bytes_val: std::ptr::null(),
            bytes_len: 0,
        }
    }
}

// ── Value enum for storage ─────────────────────────────────────────

enum Value {
    Null,
    Bool(bool),
    I64(i64),
    F64(f64),
    Str(String),
    Bytes(Vec<u8>),
}

// ── RowWriter collector ────────────────────────────────────────────

#[derive(Default)]
struct CRowCollector {
    columns: Vec<Column>,
    values: Vec<Value>,
    cols_per_row: usize,
    rows_affected: i64,
}

impl RowWriter for CRowCollector {
    fn on_metadata(&mut self, columns: &[Column]) {
        self.columns = columns.to_vec();
        self.cols_per_row = columns.len();
    }
    fn write_null(&mut self, _col: usize) {
        self.values.push(Value::Null);
    }
    fn write_bool(&mut self, _col: usize, v: bool) {
        self.values.push(Value::Bool(v));
    }
    fn write_u8(&mut self, _col: usize, v: u8) {
        self.values.push(Value::I64(v as i64));
    }
    fn write_i16(&mut self, _col: usize, v: i16) {
        self.values.push(Value::I64(v as i64));
    }
    fn write_i32(&mut self, _col: usize, v: i32) {
        self.values.push(Value::I64(v as i64));
    }
    fn write_i64(&mut self, _col: usize, v: i64) {
        self.values.push(Value::I64(v));
    }
    fn write_f32(&mut self, _col: usize, v: f32) {
        self.values.push(Value::F64(v as f64));
    }
    fn write_f64(&mut self, _col: usize, v: f64) {
        self.values.push(Value::F64(v));
    }
    fn write_str(&mut self, _col: usize, v: &str) {
        self.values.push(Value::Str(v.to_owned()));
    }
    fn write_bytes(&mut self, _col: usize, v: &[u8]) {
        self.values.push(Value::Bytes(v.to_owned()));
    }
    fn write_guid(&mut self, _col: usize, v: &[u8; 16]) {
        let u = uuid::Uuid::from_bytes(*v);
        self.values.push(Value::Str(u.to_string()));
    }
    fn write_decimal(&mut self, _col: usize, value: i128, _precision: u8, scale: u8) {
        self.values
            .push(Value::Str(decimal_to_string(value, scale)));
    }
    fn write_date(&mut self, _col: usize, unix_days: i32) {
        self.values.push(Value::Str(unix_days_to_iso(unix_days)));
    }
    fn write_time(&mut self, _col: usize, nanos: i64) {
        self.values
            .push(Value::Str(nanos_to_time_str(nanos as u64)));
    }
    fn write_datetime(&mut self, _col: usize, micros: i64) {
        self.values.push(Value::Str(micros_to_iso(micros)));
    }
    fn write_datetimeoffset(&mut self, _col: usize, micros: i64, offset_minutes: i16) {
        self.values
            .push(Value::Str(micros_offset_to_iso(micros, offset_minutes)));
    }
    fn on_done(&mut self, rows: u64) {
        self.rows_affected = rows as i64;
    }
}

// ── Type conversion helpers ────────────────────────────────────────

fn decimal_to_string(value: i128, scale: u8) -> String {
    if scale == 0 {
        return value.to_string();
    }
    let divisor = 10i128.pow(scale as u32);
    let whole = value / divisor;
    let frac = (value % divisor).unsigned_abs();
    format!("{}.{:0>width$}", whole, frac, width = scale as usize)
}

fn unix_days_to_iso(days: i32) -> String {
    let epoch = 719_163i64; // days from 0001-01-01 to 1970-01-01
    let total = epoch + days as i64;
    // Compute year/month/day from day count (proleptic Gregorian)
    let y400 = total / 146_097;
    let mut rem = total % 146_097;
    if rem < 0 {
        rem += 146_097;
    }
    let y100 = std::cmp::min(rem / 36_524, 3);
    rem -= y100 * 36_524;
    let y4 = rem / 1_461;
    rem -= y4 * 1_461;
    let y1 = std::cmp::min(rem / 365, 3);
    rem -= y1 * 365;
    let mut year = y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1;
    let leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
    let days_in_months: [i64; 12] = if leap {
        [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
    } else {
        [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
    };
    let mut month = 0u32;
    for (i, &d) in days_in_months.iter().enumerate() {
        if rem < d {
            month = i as u32 + 1;
            break;
        }
        rem -= d;
    }
    if month == 0 {
        year += 1;
        month = 1;
        rem = 0;
    }
    let day = rem + 1;
    format!("{:04}-{:02}-{:02}", year, month, day)
}

fn nanos_to_time_str(nanos: u64) -> String {
    let total_secs = nanos / 1_000_000_000;
    let h = total_secs / 3600;
    let m = (total_secs % 3600) / 60;
    let s = total_secs % 60;
    let frac = nanos % 1_000_000_000;
    if frac > 0 {
        format!("{:02}:{:02}:{:02}.{:09}", h, m, s, frac)
    } else {
        format!("{:02}:{:02}:{:02}", h, m, s)
    }
}

fn micros_to_iso(micros: i64) -> String {
    let secs = micros.div_euclid(1_000_000);
    let us = micros.rem_euclid(1_000_000);
    let days = secs.div_euclid(86400) as i32;
    let day_secs = secs.rem_euclid(86400) as u64;
    let date = unix_days_to_iso(days);
    let h = day_secs / 3600;
    let m = (day_secs % 3600) / 60;
    let s = day_secs % 60;
    if us > 0 {
        format!("{}T{:02}:{:02}:{:02}.{:06}", date, h, m, s, us)
    } else {
        format!("{}T{:02}:{:02}:{:02}", date, h, m, s)
    }
}

fn micros_offset_to_iso(micros: i64, offset_minutes: i16) -> String {
    let dt = micros_to_iso(micros);
    let sign = if offset_minutes >= 0 { '+' } else { '-' };
    let abs = offset_minutes.unsigned_abs();
    format!("{}{}{:02}:{:02}", dt, sign, abs / 60, abs % 60)
}

// ── Connection string parser ───────────────────────────────────────

fn parse_conn_str(s: &str) -> Result<Config, String> {
    let mut server = "localhost".to_string();
    let mut port: u16 = 1433;
    let mut database = "master".to_string();
    let mut user = String::new();
    let mut password = String::new();
    let mut trust_cert = false;

    for part in s.split(';') {
        let part = part.trim();
        if part.is_empty() {
            continue;
        }
        if let Some((key, val)) = part.split_once('=') {
            let key = key.trim().to_lowercase();
            let val = val.trim();
            match key.as_str() {
                "server" | "data source" => {
                    if let Some((h, p)) = val.rsplit_once(',') {
                        server = h.to_string();
                        port = p.parse().map_err(|_| "Invalid port".to_string())?;
                    } else {
                        server = val.to_string();
                    }
                }
                "database" | "initial catalog" => database = val.to_string(),
                "uid" | "user id" | "user" => user = val.to_string(),
                "pwd" | "password" => password = val.to_string(),
                "trustservercertificate" => {
                    trust_cert = val.eq_ignore_ascii_case("yes") || val.eq_ignore_ascii_case("true")
                }
                _ => {}
            }
        }
    }

    let mut config = Config::new();
    config.host(&server);
    config.port(port);
    config.database(&database);
    config.authentication(tabby::AuthMethod::sql_server(user, password));
    if trust_cert {
        config.trust_cert();
    }

    Ok(config)
}

// ── Connection handle ──────────────────────────────────────────────

type InnerClient = TdsClient<tokio_util::compat::Compat<TcpStream>>;

struct ConnectionHandle {
    client: Mutex<InnerClient>,
    runtime: Runtime,
}

// ── Result handle ──────────────────────────────────────────────────

struct ResultHandle {
    #[allow(dead_code)]
    columns: Vec<Column>,
    values: Vec<Value>,
    cols_per_row: usize,
    rows_affected: i64,
    /// CStrings kept alive for column names
    column_names: Vec<CString>,
    /// CString kept alive for string values returned by get_value
    last_str: Mutex<Option<CString>>,
}

impl ResultHandle {
    fn row_count(&self) -> i64 {
        if self.cols_per_row == 0 {
            self.rows_affected
        } else {
            (self.values.len() / self.cols_per_row) as i64
        }
    }
}

// ── C ABI exports ──────────────────────────────────────────────────

#[no_mangle]
pub extern "C" fn nuzzle_connect(conn_str: *const c_char) -> *mut ConnectionHandle {
    let c_str = unsafe {
        if conn_str.is_null() {
            set_error("Connection string is null");
            return std::ptr::null_mut();
        }
        CStr::from_ptr(conn_str)
    };

    let conn_str = match c_str.to_str() {
        Ok(s) => s,
        Err(e) => {
            set_error(&format!("Invalid UTF-8: {e}"));
            return std::ptr::null_mut();
        }
    };

    let config = match parse_conn_str(conn_str) {
        Ok(c) => c,
        Err(e) => {
            set_error(&e);
            return std::ptr::null_mut();
        }
    };

    let runtime = match Runtime::new() {
        Ok(r) => r,
        Err(e) => {
            set_error(&format!("Failed to create runtime: {e}"));
            return std::ptr::null_mut();
        }
    };

    let client = runtime.block_on(async {
        TdsClient::connect_with_redirect(config, |host, port| async move {
            let addr = format!("{host}:{port}");
            let tcp = TcpStream::connect(&addr)
                .await
                .map_err(|e| Box::new(e) as Box<dyn std::error::Error + Send + Sync>)?;
            tcp.set_nodelay(true)
                .map_err(|e| Box::new(e) as Box<dyn std::error::Error + Send + Sync>)?;
            Ok(tcp.compat_write())
        })
        .await
    });

    match client {
        Ok(c) => Box::into_raw(Box::new(ConnectionHandle {
            client: Mutex::new(c),
            runtime,
        })),
        Err(e) => {
            set_error(&format!("Connection failed: {e}"));
            std::ptr::null_mut()
        }
    }
}

#[no_mangle]
pub extern "C" fn nuzzle_query(
    handle: *mut ConnectionHandle,
    sql: *const c_char,
) -> *mut ResultHandle {
    let handle = unsafe {
        if handle.is_null() {
            set_error("Handle is null");
            return std::ptr::null_mut();
        }
        &*handle
    };

    let sql_str = unsafe {
        if sql.is_null() {
            set_error("SQL is null");
            return std::ptr::null_mut();
        }
        match CStr::from_ptr(sql).to_str() {
            Ok(s) => s.to_owned(),
            Err(e) => {
                set_error(&format!("Invalid UTF-8: {e}"));
                return std::ptr::null_mut();
            }
        }
    };

    let mut client = match handle.client.lock() {
        Ok(c) => c,
        Err(e) => {
            set_error(&format!("Lock poisoned: {e}"));
            return std::ptr::null_mut();
        }
    };

    let mut writer = CRowCollector::default();

    let result = handle
        .runtime
        .block_on(client.batch_into(&sql_str, &mut writer));

    match result {
        Ok(()) => {
            let column_names: Vec<CString> = writer
                .columns
                .iter()
                .map(|c| CString::new(c.name()).unwrap_or_default())
                .collect();

            Box::into_raw(Box::new(ResultHandle {
                columns: writer.columns,
                values: writer.values,
                cols_per_row: writer.cols_per_row,
                rows_affected: writer.rows_affected,
                column_names,
                last_str: Mutex::new(None),
            }))
        }
        Err(e) => {
            set_error(&format!("Query failed: {e}"));
            std::ptr::null_mut()
        }
    }
}

#[no_mangle]
pub extern "C" fn nuzzle_result_column_count(result: *const ResultHandle) -> i32 {
    if result.is_null() {
        return 0;
    }
    unsafe { (*result).cols_per_row as i32 }
}

#[no_mangle]
pub extern "C" fn nuzzle_result_column_name(
    result: *const ResultHandle,
    idx: i32,
) -> *const c_char {
    if result.is_null() {
        return std::ptr::null();
    }
    let r = unsafe { &*result };
    if idx < 0 || (idx as usize) >= r.column_names.len() {
        return std::ptr::null();
    }
    r.column_names[idx as usize].as_ptr()
}

#[no_mangle]
pub extern "C" fn nuzzle_result_row_count(result: *const ResultHandle) -> i64 {
    if result.is_null() {
        return 0;
    }
    unsafe { (*result).row_count() }
}

#[no_mangle]
pub extern "C" fn nuzzle_result_get_value(
    result: *const ResultHandle,
    row: i64,
    col: i32,
) -> NuzzleValue {
    let mut out = NuzzleValue::default();
    if result.is_null() {
        return out;
    }
    let r = unsafe { &*result };
    if col < 0 || (col as usize) >= r.cols_per_row {
        return out;
    }
    let idx = (row as usize) * r.cols_per_row + col as usize;
    if idx >= r.values.len() {
        return out;
    }

    match &r.values[idx] {
        Value::Null => {
            out.type_tag = NUZZLE_TYPE_NULL;
        }
        Value::Bool(v) => {
            out.type_tag = NUZZLE_TYPE_BOOL;
            out.int_val = *v as i64;
        }
        Value::I64(v) => {
            out.type_tag = NUZZLE_TYPE_I64;
            out.int_val = *v;
        }
        Value::F64(v) => {
            out.type_tag = NUZZLE_TYPE_F64;
            out.float_val = *v;
        }
        Value::Str(s) => {
            out.type_tag = NUZZLE_TYPE_STRING;
            out.str_len = s.len() as i32;
            // Store CString in last_str to keep it alive
            if let Ok(cs) = CString::new(s.as_str()) {
                out.str_val = cs.as_ptr();
                if let Ok(mut guard) = r.last_str.lock() {
                    *guard = Some(cs);
                }
            }
        }
        Value::Bytes(b) => {
            out.type_tag = NUZZLE_TYPE_BYTES;
            out.bytes_val = b.as_ptr();
            out.bytes_len = b.len() as i32;
        }
    }

    out
}

#[no_mangle]
pub extern "C" fn nuzzle_result_rows_affected(result: *const ResultHandle) -> i64 {
    if result.is_null() {
        return 0;
    }
    unsafe { (*result).rows_affected }
}

#[no_mangle]
pub extern "C" fn nuzzle_result_free(result: *mut ResultHandle) {
    if !result.is_null() {
        unsafe {
            drop(Box::from_raw(result));
        }
    }
}

#[no_mangle]
pub extern "C" fn nuzzle_close(handle: *mut ConnectionHandle) {
    if !handle.is_null() {
        unsafe {
            drop(Box::from_raw(handle));
        }
    }
}
