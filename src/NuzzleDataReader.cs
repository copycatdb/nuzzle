using System.Collections;
using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;

namespace CopycatDb.Nuzzle;

public class NuzzleDataReader : DbDataReader
{
    private readonly IntPtr _result;
    private readonly int _columnCount;
    private readonly long _rowCount;
    private readonly string[] _columnNames;
    private long _currentRow = -1;
    private bool _closed;

    internal NuzzleDataReader(IntPtr result)
    {
        _result = result;
        _columnCount = NativeBindings.nuzzle_result_column_count(result);
        _rowCount = NativeBindings.nuzzle_result_row_count(result);

        _columnNames = new string[_columnCount];
        for (int i = 0; i < _columnCount; i++)
        {
            var ptr = NativeBindings.nuzzle_result_column_name(result, i);
            _columnNames[i] = ptr == IntPtr.Zero ? $"col{i}" : Marshal.PtrToStringUTF8(ptr) ?? $"col{i}";
        }
    }

    public override int FieldCount => _columnCount;
    public override int Depth => 0;
    public override bool HasRows => _rowCount > 0;
    public override bool IsClosed => _closed;
    public override int RecordsAffected => (int)NativeBindings.nuzzle_result_rows_affected(_result);

    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));

    public override bool Read()
    {
        if (_closed) return false;
        _currentRow++;
        return _currentRow < _rowCount;
    }

    public override bool NextResult() => false;

    public override void Close()
    {
        if (!_closed)
        {
            _closed = true;
            NativeBindings.nuzzle_result_free(_result);
        }
    }

    protected override void Dispose(bool disposing)
    {
        Close();
        base.Dispose(disposing);
    }

    public override string GetName(int ordinal) => _columnNames[ordinal];

    public override int GetOrdinal(string name)
    {
        for (int i = 0; i < _columnNames.Length; i++)
            if (string.Equals(_columnNames[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new IndexOutOfRangeException($"Column '{name}' not found.");
    }

    public override object GetValue(int ordinal)
    {
        var val = NativeBindings.nuzzle_result_get_value(_result, _currentRow, ordinal);
        return val.TypeTag switch
        {
            NativeBindings.TypeNull => DBNull.Value,
            NativeBindings.TypeBool => val.IntVal != 0,
            NativeBindings.TypeI64 => val.IntVal,
            NativeBindings.TypeF64 => val.FloatVal,
            NativeBindings.TypeString => val.StrVal == IntPtr.Zero
                ? DBNull.Value
                : Marshal.PtrToStringUTF8(val.StrVal) ?? string.Empty,
            NativeBindings.TypeBytes => ReadBytes(val),
            _ => DBNull.Value
        };
    }

    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, _columnCount);
        for (int i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }

    public override bool IsDBNull(int ordinal)
    {
        var val = NativeBindings.nuzzle_result_get_value(_result, _currentRow, ordinal);
        return val.TypeTag == NativeBindings.TypeNull;
    }

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
    public override byte GetByte(int ordinal) => (byte)(long)GetValue(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => ((string)GetValue(ordinal))[0];
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override DateTime GetDateTime(int ordinal) => DateTime.Parse((string)GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => decimal.Parse(GetValue(ordinal).ToString()!);
    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
    public override float GetFloat(int ordinal) => (float)(double)GetValue(ordinal);
    public override Guid GetGuid(int ordinal) => Guid.Parse((string)GetValue(ordinal));
    public override short GetInt16(int ordinal) => (short)(long)GetValue(ordinal);
    public override int GetInt32(int ordinal) => (int)(long)GetValue(ordinal);
    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
    public override string GetString(int ordinal) => (string)GetValue(ordinal);

    public override string GetDataTypeName(int ordinal) => "variant";
    public override Type GetFieldType(int ordinal)
    {
        var val = NativeBindings.nuzzle_result_get_value(_result, _currentRow, ordinal);
        return val.TypeTag switch
        {
            NativeBindings.TypeBool => typeof(bool),
            NativeBindings.TypeI64 => typeof(long),
            NativeBindings.TypeF64 => typeof(double),
            NativeBindings.TypeString => typeof(string),
            NativeBindings.TypeBytes => typeof(byte[]),
            _ => typeof(object)
        };
    }

    public override IEnumerator GetEnumerator() => new DbEnumerator(this);

    private static object ReadBytes(NuzzleNativeValue val)
    {
        if (val.BytesVal == IntPtr.Zero || val.BytesLen <= 0) return DBNull.Value;
        var buf = new byte[val.BytesLen];
        Marshal.Copy(val.BytesVal, buf, 0, val.BytesLen);
        return buf;
    }
}
