using CopycatDb.Nuzzle;
using Xunit;

namespace Nuzzle.Tests;

public class ConnectionTests
{
    private static string ConnStr =>
        Environment.GetEnvironmentVariable("NUZZLE_CONNECTION_STRING")
        ?? "Server=localhost,1433;User Id=sa;Password=Nuzzle_Test_P@ss1;Database=master;TrustServerCertificate=yes";

    [Fact]
    public void Connect_And_Close()
    {
        using var conn = new NuzzleConnection(ConnStr);
        conn.Open();
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        conn.Close();
        Assert.Equal(System.Data.ConnectionState.Closed, conn.State);
    }

    [Fact]
    public void Open_Twice_Is_Idempotent()
    {
        using var conn = new NuzzleConnection(ConnStr);
        conn.Open();
        conn.Open(); // should not throw
        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [Fact]
    public void Bad_Connection_String_Throws()
    {
        using var conn = new NuzzleConnection("Server=127.0.0.1,19999;User Id=sa;Password=bad;TrustServerCertificate=yes");
        Assert.Throws<NuzzleException>(() => conn.Open());
    }
}
