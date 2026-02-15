using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace CopycatDb.Nuzzle;

public class NuzzleConnection : DbConnection
{
    private string _connectionString = string.Empty;
    private ConnectionState _state = ConnectionState.Closed;
    internal IntPtr NativeHandle { get; private set; }

    public NuzzleConnection() { }

    public NuzzleConnection(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    [AllowNull]
    public override string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }

    public override string Database => ParseValue("database") ?? ParseValue("initial catalog") ?? "master";
    public override string DataSource => ParseValue("server") ?? ParseValue("data source") ?? "localhost";
    public override string ServerVersion => "tabby";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) =>
        throw new NotSupportedException("Use a new connection to change database.");

    public override void Open()
    {
        if (_state == ConnectionState.Open) return;

        NativeHandle = NativeBindings.nuzzle_connect(_connectionString);
        if (NativeHandle == IntPtr.Zero)
            throw NuzzleException.FromNative();

        _state = ConnectionState.Open;
    }

    public override void Close()
    {
        if (_state == ConnectionState.Closed) return;
        if (NativeHandle != IntPtr.Zero)
        {
            NativeBindings.nuzzle_close(NativeHandle);
            NativeHandle = IntPtr.Zero;
        }
        _state = ConnectionState.Closed;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException("Transactions not yet supported.");

    protected override DbCommand CreateDbCommand() => new NuzzleCommand { Connection = this };

    protected override void Dispose(bool disposing)
    {
        Close();
        base.Dispose(disposing);
    }

    private string? ParseValue(string key)
    {
        foreach (var part in _connectionString.Split(';'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return null;
    }
}
