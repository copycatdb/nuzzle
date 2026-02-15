using CopycatDb.Nuzzle;
using Xunit;

namespace Nuzzle.Tests;

public class DmlTests
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
    public void Insert_Update_Delete()
    {
        using var conn = OpenConn();

        // Setup
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                IF OBJECT_ID('tempdb..##nuzzle_dml_test') IS NOT NULL DROP TABLE ##nuzzle_dml_test;
                CREATE TABLE ##nuzzle_dml_test (id INT IDENTITY PRIMARY KEY, name NVARCHAR(100));";
            cmd.ExecuteNonQuery();
        }

        // Insert
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO ##nuzzle_dml_test (name) VALUES (N'Alice'), (N'Bob')";
            var rows = cmd.ExecuteNonQuery();
            Assert.Equal(2, rows);
        }

        // Update
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE ##nuzzle_dml_test SET name = N'Charlie' WHERE name = N'Bob'";
            var rows = cmd.ExecuteNonQuery();
            Assert.Equal(1, rows);
        }

        // Select to verify
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM ##nuzzle_dml_test ORDER BY id";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("Alice", reader.GetString(0));
            Assert.True(reader.Read());
            Assert.Equal("Charlie", reader.GetString(0));
            Assert.False(reader.Read());
        }

        // Delete
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DELETE FROM ##nuzzle_dml_test WHERE name = N'Alice'";
            var rows = cmd.ExecuteNonQuery();
            Assert.Equal(1, rows);
        }

        // Cleanup
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "DROP TABLE ##nuzzle_dml_test";
            cmd.ExecuteNonQuery();
        }
    }
}
