# Sharp-DB

A CLI tool and Claude Code Skill for querying databases (PostgreSQL, MySQL, SQLite) and inspecting schema metadata.

## Features

- **Execute SQL queries** and get results as markdown tables
- **List tables and views** with primary keys, foreign keys, and descriptions
- **Inspect table columns** with data types, constraints, and foreign key references
- **Multi-database support**: PostgreSQL, MySQL, SQLite
- **Schema-aware**: Optional schema filtering for databases that support schemas

## Installation

```bash
cd /Users/zhang/Developer/dotnet/sharp-db-mcp
dotnet build src/SharpDbMcp/SharpDbMcp.csproj
```

The binary is at `src/SharpDbMcp/bin/Debug/net10.0/sharp-db`.

## Usage

### Query — Execute SQL

```bash
sharp-db query --db-type postgres --connection "host=localhost;port=5432;database=mydb;username=postgres;password=pass" --sql "SELECT id, name FROM users"
```

### Tables — List tables and views

```bash
sharp-db tables --db-type postgres --connection "host=localhost;port=5432;database=mydb;username=postgres;password=pass"
```

Optional `--schema` parameter to filter by schema:

```bash
sharp-db tables --db-type postgres --connection "..." --schema public
```

### Columns — Inspect table columns

```bash
sharp-db columns --db-type postgres --connection "..." --table users
```

Optional `--schema` parameter:

```bash
sharp-db columns --db-type postgres --connection "..." --table users --schema public
```

## As a Claude Code Skill

This project includes a Skill at `.claude/skills/sharp-db/SKILL.md`. When working in Claude Code, you can use `/sharp-db` to invoke database queries directly.

### Example usage in Claude Code

```
/sharp-db query --db-type sqlite --connection "Data Source=test.db" --sql "SELECT * FROM users"
```

## Supported databases

| Database | Driver | Connection string example |
|----------|--------|---------------------------|
| PostgreSQL | Npgsql | `host=localhost;port=5432;database=mydb;username=postgres;password=pass` |
| MySQL | MySql.Data | `server=localhost;port=3306;database=mydb;user=root;password=pass` |
| SQLite | Microsoft.Data.Sqlite | `Data Source=/path/to/db.sqlite` |

## Development

### Build

```bash
dotnet build
```

### Test

```bash
dotnet test
```

### Project structure

```
src/SharpDbMcp/
├── Metadata/
│   ├── IMetadataProvider.cs         # Provider interface
│   ├── BaseMetadataProvider.cs      # Shared execution logic
│   ├── PostgresMetadataProvider.cs  # PostgreSQL SQL
│   ├── MySQLMetadataProvider.cs     # MySQL SQL
│   ├── SqliteMetadataProvider.cs    # SQLite SQL
│   └── MetadataProviderFactory.cs   # Factory by dbType
├── DatabaseOptions.cs               # Connection parameters
├── DbConnectionFactory.cs           # Connection creation
├── MarkdownTableFormatter.cs        # Result formatting
├── MetadataQueryService.cs          # Metadata queries
├── Program.cs                       # CLI entry point
└── QueryExecutor.cs                 # SQL execution

test/SharpDbMcpTest/
└── QueryExecutorTests.cs            # Tests (SQLite in-memory)
```

## License

MIT