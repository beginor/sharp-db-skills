---
name: sharp-db
description: Query databases (PostgreSQL, MySQL, SQLite) and inspect schema metadata. Use when the user wants to run SQL queries, list tables/views, or inspect table columns against any database. Requires building the project first and the user to provide a database type and connection string.
tools: Bash, Read, Glob
---

# Sharp-DB

A CLI tool for querying databases and inspecting schema metadata. Supports PostgreSQL, MySQL, and SQLite.

## Requirements

You should have dotnet sdk 10.0.x installed. if `dotnet` does not exist on system, please install it first:

- macOS/Linux: `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash`
- Windows: `irm https://dot.net/v1/dotnet-install.ps1 | iex`

## Setup

Build the tool once before first use:

```bash
git clone https://github.com/beginor/sharp-db.git
cd sharp-db
dotnet publish src/SharpDb/SharpDb.csproj \
  --self-contained \
  --configuration Release \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:PublishTrimmed=true \
  -p:PublishReadyToRun=false \
  -p:DebugType=None \
  --output bin
```

The binary is at `bin/sharp-db` (or `bin/sharp-db.exe` on windows).

## Commands

### query — Execute SQL

Run a SQL statement and return results as a markdown table.

```bash
sharp-db query --db-type <postgres|mysql|sqlite> --connection "<conn-string>" --sql "<sql>"
```

For non-SELECT statements (INSERT, UPDATE, DELETE), returns `Rows affected: N`.

**Confirmation required for mutating statements.** Before running any SQL that modifies data (INSERT, UPDATE, DELETE, MERGE, REPLACE, TRUNCATE, CREATE, DROP, ALTER, etc.), you MUST present the SQL to the user and ask for explicit confirmation. Only proceed after the user agrees. SELECT and other read-only statements do not require confirmation.

### tables — List tables and views

List all tables and views with metadata (primary keys, foreign keys, descriptions, related objects).

```bash
sharp-db tables --db-type <postgres|mysql|sqlite> --connection "<conn-string>" [--schema <name>]
```

If `--schema` is omitted and the database supports schemas, returns tables from all schemas.

### columns — Inspect table columns

List columns for a specific table or view, including data types, constraints, nullability, and foreign key references.

```bash
sharp-db columns --db-type <postgres|mysql|sqlite> --connection "<conn-string>" --table <name> [--schema <name>]
```

### execute — Execute a SQL file

Execute a SQL file within a transaction. Requires interactive confirmation before running. Rolls back on error.

```bash
sharp-db execute --db-type <postgres|mysql|sqlite> --connection "<conn-string>" --file <path-to-sql-file>
```

The tool prompts `Execute? [y/N]` before running. Only `y` or `yes` proceeds; anything else aborts. stdin must be a terminal (redirected stdin is rejected for safety).

## Connection string patterns

| Database | Example |
|----------|---------|
| PostgreSQL | `host=localhost;port=5432;database=mydb;username=postgres;password=pass` |
| PostgreSQL | `server=127.0.0.1;port=5432;database=test_db;user id=postgres;password=pgsql@18` |
| MySQL | `server=localhost;port=3306;database=mydb;user=root;password=pass` |
| SQLite | `Data Source=/path/to/db.sqlite` |
| SQLite | `Data Source=:memory:` |

## Workflow

1. **Identify the database type** — Ask the user or infer from context (PostgreSQL, MySQL, or SQLite).
2. **Obtain the connection string** — Ask the user for connection details if not provided.
3. **Choose the command** — `query` for SQL execution, `tables` for schema listing, `columns` for column inspection, `execute` for running SQL files.
4. **Run and present** — Execute via Bash and present the markdown output to the user.
5. **Chain for exploration** — For multi-step tasks (list tables → inspect columns → run query), chain commands naturally.
6. **Confirm before execute** — When using `execute`, the tool will prompt for confirmation. Ensure the user understands the SQL file will run within a transaction.

## Examples

### Run a query

```bash
sharp-db query --db-type postgres --connection "host=localhost;port=5432;database=mydb;username=postgres;password=pass" --sql "SELECT count(*) FROM users"
```

### List all tables

```bash
sharp-db tables --db-type mysql --connection "server=localhost;port=3306;database=mydb;user=root;password=pass"
```

### Filter tables by schema

```bash
sharp-db tables --db-type postgres --connection "host=localhost;port=5432;database=mydb;username=postgres;password=pass" --schema public
```

### Inspect columns of a table

```bash
sharp-db columns --db-type sqlite --connection "Data Source=test.db" --table users
```

### Execute an update

```bash
sharp-db query --db-type postgres --connection "host=localhost;port=5432;database=mydb;username=postgres;password=pass" --sql "UPDATE users SET active = true WHERE id = 1"
```

## Error handling

- If the tool returns an error, present the error message to the user and suggest checking the connection string or database type.
- If a table or column is not found, inform the user and suggest running `tables` or `columns` to discover available names.
- If the database is unreachable, suggest verifying network connectivity and credentials.

## Notes

- SQLite uses in-memory databases when `Data Source=:memory:` is specified; each connection creates a new database.
- PostgreSQL and MySQL queries include table descriptions (comments) when available.
- Foreign key information includes the referenced table and column for easy relationship tracing.
