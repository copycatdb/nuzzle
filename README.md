# nuzzle

**High-performance ADO.NET provider for SQL Server, powered by [tabby](https://github.com/copycatdb/tabby) (Rust TDS protocol).**

Part of the [CopyCat](https://github.com/copycatdb) SQL Server ecosystem.

## Why nuzzle?

- **Linux-first** — built for modern .NET on Linux
- **Fast** — Rust native library handles the TDS wire protocol via tabby
- **ADO.NET compatible** — works with Dapper, Entity Framework Core, and anything that uses `DbConnection`
- **Modern .NET only** — targets .NET 8+ (no .NET Framework baggage)

## Architecture

```
┌──────────────────────┐
│   Your .NET App      │
│  (Dapper, EF Core)   │
├──────────────────────┤
│  Nuzzle ADO.NET      │  ← C# DbConnection/DbCommand/DbDataReader
│  Provider            │
├──────────────────────┤
│  P/Invoke (C ABI)    │
├──────────────────────┤
│  nuzzle-native       │  ← Rust cdylib
│  (tabby TDS client)  │
├──────────────────────┤
│  SQL Server          │
└──────────────────────┘
```

## Quick Start

### Build the native library

```bash
cd native
cargo build --release
```

The shared library will be at `native/target/release/libnuzzle_native.so` (Linux).

### Use in your .NET project

```csharp
using CopycatDb.Nuzzle;

using var connection = new NuzzleConnection(
    "Server=localhost,1433;User Id=sa;Password=yourpassword;Database=mydb;TrustServerCertificate=yes"
);
connection.Open();

using var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT Id, Name FROM Users";

using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"{reader.GetInt64(0)}: {reader.GetString(1)}");
}
```

### Connection string

Compatible with `Microsoft.Data.SqlClient` format:

| Key | Description |
|-----|-------------|
| `Server` | Host and port, e.g. `localhost,1433` |
| `User Id` / `UID` | SQL Server login |
| `Password` / `PWD` | Password |
| `Database` / `Initial Catalog` | Database name |
| `TrustServerCertificate` | `yes` to skip TLS cert validation |

## Running Tests

```bash
# Start SQL Server (Docker)
docker run -d -p 1433:1433 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD='Nuzzle_Test_P@ss1' \
  mcr.microsoft.com/mssql/server:2022-latest

# Build native lib
cd native && cargo build --release && cd ..

# Set library path
export LD_LIBRARY_PATH=$PWD/native/target/release

# Run tests
cd test && dotnet test
```

## Benchmarks

_Coming soon._

## License

MIT
