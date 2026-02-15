using System.Runtime.InteropServices;

namespace CopycatDb.Nuzzle;

[StructLayout(LayoutKind.Sequential)]
internal struct NuzzleNativeValue
{
    public int TypeTag;
    public long IntVal;
    public double FloatVal;
    public IntPtr StrVal;
    public int StrLen;
    public IntPtr BytesVal;
    public int BytesLen;
}

/// <summary>
/// P/Invoke bindings to the nuzzle native library (Rust/tabby).
/// </summary>
internal static class NativeBindings
{
    private const string LibName = "nuzzle_native";

    public const int TypeNull = 0;
    public const int TypeBool = 1;
    public const int TypeI64 = 2;
    public const int TypeF64 = 3;
    public const int TypeString = 4;
    public const int TypeBytes = 5;

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nuzzle_connect([MarshalAs(UnmanagedType.LPUTF8Str)] string connStr);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nuzzle_query(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nuzzle_result_column_count(IntPtr result);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nuzzle_result_column_name(IntPtr result, int idx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long nuzzle_result_row_count(IntPtr result);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern NuzzleNativeValue nuzzle_result_get_value(IntPtr result, long row, int col);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern long nuzzle_result_rows_affected(IntPtr result);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nuzzle_result_free(IntPtr result);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nuzzle_close(IntPtr handle);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nuzzle_last_error();

    public static string? GetLastError()
    {
        var ptr = nuzzle_last_error();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
