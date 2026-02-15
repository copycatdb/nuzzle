using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace CopycatDb.Nuzzle;

public class NuzzleCommand : DbCommand
{
    private string _commandText = string.Empty;
    private NuzzleParameterCollection _parameters = new();

    public NuzzleCommand() { }

    public NuzzleCommand(string commandText, NuzzleConnection? connection = null)
    {
        _commandText = commandText;
        DbConnection = connection;
    }

    [AllowNull]
    public override string CommandText
    {
        get => _commandText;
        set => _commandText = value ?? string.Empty;
    }

    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public new NuzzleParameterCollection Parameters => _parameters;

    public override void Cancel() { }
    public override void Prepare() { }

    public override int ExecuteNonQuery()
    {
        var conn = GetConnection();
        var sql = BuildSql();
        var result = NativeBindings.nuzzle_query(conn.NativeHandle, sql);
        if (result == IntPtr.Zero)
            throw NuzzleException.FromNative();

        var affected = NativeBindings.nuzzle_result_rows_affected(result);
        NativeBindings.nuzzle_result_free(result);
        return (int)affected;
    }

    public override object? ExecuteScalar()
    {
        using var reader = ExecuteDbDataReader(CommandBehavior.Default);
        if (reader.Read() && reader.FieldCount > 0)
            return reader.GetValue(0);
        return null;
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var conn = GetConnection();
        var sql = BuildSql();
        var result = NativeBindings.nuzzle_query(conn.NativeHandle, sql);
        if (result == IntPtr.Zero)
            throw NuzzleException.FromNative();

        return new NuzzleDataReader(result);
    }

    protected override DbParameter CreateDbParameter() => new NuzzleParameter();

    private NuzzleConnection GetConnection()
    {
        if (DbConnection is not NuzzleConnection conn)
            throw new InvalidOperationException("Connection is not set or not a NuzzleConnection.");
        if (conn.State != ConnectionState.Open)
            throw new InvalidOperationException("Connection is not open.");
        return conn;
    }

    private string BuildSql()
    {
        var sql = _commandText;
        // Inline parameters: replace @name with literal values
        foreach (NuzzleParameter p in _parameters)
        {
            var placeholder = p.ParameterName.StartsWith('@') ? p.ParameterName : "@" + p.ParameterName;
            sql = sql.Replace(placeholder, p.ToSqlLiteral());
        }
        return sql;
    }
}
