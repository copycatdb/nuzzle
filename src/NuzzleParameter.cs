using System.Collections;
using System.Data.Common;

namespace CopycatDb.Nuzzle;

public class NuzzleParameter : DbParameter
{
    public override string ParameterName { get; set; } = string.Empty;
    public override object? Value { get; set; }
    public override System.Data.DbType DbType { get; set; }
    public override System.Data.ParameterDirection Direction { get; set; } = System.Data.ParameterDirection.Input;
    public override bool IsNullable { get; set; }
    public override int Size { get; set; }
    public override string SourceColumn { get; set; } = string.Empty;
    public override bool SourceColumnNullMapping { get; set; }

    public override void ResetDbType() => DbType = System.Data.DbType.String;

    internal string ToSqlLiteral()
    {
        if (Value is null || Value is DBNull) return "NULL";
        return Value switch
        {
            bool b => b ? "1" : "0",
            int i => i.ToString(),
            long l => l.ToString(),
            short s => s.ToString(),
            byte b => b.ToString(),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            decimal dc => dc.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string s => $"N'{s.Replace("'", "''")}'",
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'",
            _ => $"N'{Value.ToString()?.Replace("'", "''") ?? "NULL"}'"
        };
    }
}

public class NuzzleParameterCollection : DbParameterCollection
{
    private readonly List<NuzzleParameter> _params = new();

    public override int Count => _params.Count;
    public override object SyncRoot => ((ICollection)_params).SyncRoot;

    public override int Add(object value)
    {
        _params.Add((NuzzleParameter)value);
        return _params.Count - 1;
    }

    public NuzzleParameter Add(string name, object? value)
    {
        var p = new NuzzleParameter { ParameterName = name, Value = value };
        _params.Add(p);
        return p;
    }

    public override void AddRange(Array values)
    {
        foreach (NuzzleParameter p in values) _params.Add(p);
    }

    public override void Clear() => _params.Clear();
    public override bool Contains(object value) => _params.Contains((NuzzleParameter)value);
    public override bool Contains(string value) => _params.Any(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => ((ICollection)_params).CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _params.GetEnumerator();
    public override int IndexOf(object value) => _params.IndexOf((NuzzleParameter)value);
    public override int IndexOf(string parameterName) => _params.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _params.Insert(index, (NuzzleParameter)value);
    public override void Remove(object value) => _params.Remove((NuzzleParameter)value);
    public override void RemoveAt(int index) => _params.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _params.RemoveAll(p => p.ParameterName == parameterName);

    protected override DbParameter GetParameter(int index) => _params[index];
    protected override DbParameter GetParameter(string parameterName) =>
        _params.First(p => p.ParameterName == parameterName);
    protected override void SetParameter(int index, DbParameter value) => _params[index] = (NuzzleParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value)
    {
        var idx = IndexOf(parameterName);
        if (idx >= 0) _params[idx] = (NuzzleParameter)value;
    }
}
