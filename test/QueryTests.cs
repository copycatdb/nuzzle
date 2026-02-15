using CopycatDb.Nuzzle;
using Xunit;

namespace Nuzzle.Tests;

public class QueryTests
{
    private static string ConnStr =>
        Environment.GetEnvironmentVariable("NUZZLE_CONNECTION_STRING")
        ?? "Server=localhost,1433;User Id=sa;Password=Nuzzle_Test_P@ss1;Database=master;TrustServerCertificate=yes";

    private NuzzleConnection OpenConn()
    {
        var conn = new NuzzleConnection(ConnStr);
        conn.Open();
        return conn;
    }

    [Fact]
    public void Select_Scalar_Int()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 42 AS val";
        var result = cmd.ExecuteScalar();
        Assert.Equal(42L, result);
    }

    [Fact]
    public void Select_Scalar_String()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT N'hello' AS val";
        var result = cmd.ExecuteScalar();
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Select_Multiple_Columns()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 AS a, N'two' AS b, 3.14 AS c";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(3, reader.FieldCount);
        Assert.Equal("a", reader.GetName(0));
        Assert.Equal("b", reader.GetName(1));
        Assert.Equal("c", reader.GetName(2));
    }

    [Fact]
    public void Select_Null()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NULL AS val";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
    }

    [Fact]
    public void Select_Multiple_Rows()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT v FROM (VALUES (1),(2),(3)) AS t(v)";
        using var reader = cmd.ExecuteReader();
        var values = new List<long>();
        while (reader.Read())
            values.Add(reader.GetInt64(0));
        Assert.Equal(new long[] { 1, 2, 3 }, values.ToArray());
    }

    [Fact]
    public void Select_Bit()
    {
        using var conn = OpenConn();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CAST(1 AS BIT) AS val";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.GetBoolean(0));
    }
}
