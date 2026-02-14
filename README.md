# nuzzle 💜

.NET ADO.NET driver for SQL Server. Rubs up against ADO.NET just right.

Part of [CopyCat](https://github.com/copycatdb) 🐱

## What is this?

An ADO.NET data provider for SQL Server using [tabby](https://github.com/copycatdb/tabby) via P/Invoke. Implements `DbConnection`, `DbCommand`, `DbDataReader` — the whole nine yards.

```csharp
await using var conn = new CopyCatConnection("Server=localhost,1433;UID=sa;PWD=pass;TrustServerCertificate=yes");
await conn.OpenAsync();

await using var cmd = new CopyCatCommand("SELECT * FROM users WHERE id = @id", conn);
cmd.Parameters.AddWithValue("@id", 42);

await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine(reader.GetString("name"));
}
```

## The irony

Microsoft.Data.SqlClient is Microsofts own driver for Microsofts own database on Microsofts own framework. And were here offering an alternative. With a cat. Written in Rust.

We mean this with the utmost respect — SqlClient is an incredible driver that has served millions of developers for decades. nuzzle is just what happens when you start fresh with no backward-compatibility constraints.

## Status

🚧 Coming soon.

## Attribution

Inspired by [Npgsql](https://github.com/npgsql/npgsql), the .NET PostgreSQL driver that proves ADO.NET can be pleasant. And by [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient) — a monument of engineering and dedication to every customer.

## License

MIT
