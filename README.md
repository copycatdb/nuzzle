# purr 💜

.NET ADO.NET driver for SQL Server. Npgsql with a SQL Server accent.

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

Microsoft.Data.SqlClient is *Microsofts own driver* for *Microsofts own database* on *Microsofts own framework*. And were replacing it. With a cat. Written in Rust.

The audacity is not lost on us.

## Status

🚧 Coming soon.

## Attribution

Inspired by [Npgsql](https://github.com/npgsql/npgsql), the .NET PostgreSQL driver that proves ADO.NET can actually be pleasant. And by [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient) — we salute your decades of service. Enjoy retirement.

## License

MIT
