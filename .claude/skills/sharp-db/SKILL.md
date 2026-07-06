---
name: sharp-db
description: Query databases (PostgreSQL, MySQL, SQLite) and inspect schema metadata. Use when the user wants to run SQL queries, list tables/views, or inspect table columns against any database. Requires the user to provide a database type and connection string.
tools: Bash
---

# Sharp-DB

A CLI tool for querying databases and inspecting schema metadata. Supports PostgreSQL, MySQL, and SQLite.

## When to use this skill

- User wants to run a SQL query against a database
- User wants to list tables or views in a database
- User wants to inspect columns of a specific table
- User asks about database schema, primary keys, foreign keys, or column types

## Prerequisites

The tool must be built first. Run once:

```bash
cd /Users/zhang/Developer/dotnet/sharp-db-mcp && dotnet build src/SharpDbMcp/SharpDbMcp.csproj
```

The binary is at `src/SharpDbMcp/bin/Debug/net10.0/sharp-db`.

## Commands

### Query — Execute SQL

```bash
sharp-db query --db-type <postgres|mysql|sqlite> --connection "<conn-string>" --sql "<sql>"
```

Returns results as a markdown table. For non-SELECT statements, returns "Rows affected: N".

### Tables — List tables and views

```bash
sharp-db tables --db-type <postgres|mysql|sqlite> --connection "<conn-string>" [--schema <name>]
```

Returns a markdown table with columns: `table_schema`, `table_name`, `table_type`, `table_description`, `primary_key_columns`, `foreign_keys`, `related_objects`.

If `--schema` is omitted and the database supports schemas, returns tables from all schemas.

### Columns — Inspect table columns

```bash
sharp-db columns --db-type <postgres|mysql|sqlite> --connection "<conn-string>" --table <name> [--schema <name>]
```

Returns a markdown table with columns: `table_schema`, `table_name`, `column_name`, `ordinal_position`, `data_type`, `is_nullable`, `column_default`, `column_description`, `is_primary_key`, `is_foreign_key`, `referenced_table_schema`, `referenced_table_name`, `referenced_column_name`.

## Common connection string patterns

| Database | Example |
|----------|---------|
| PostgreSQL | `host=localhost;port=5432;database=mydb;username=postgres;password=pass` |
| MySQL | `server=localhost;port=3306;database=mydb;user=root;password=pass` |
| SQLite | `Data Source=/path/to/db.sqlite` |

## Workflow

1. Identify the database type and obtain the connection string from the user.
2. Choose the appropriate command (`query`, `tables`, or `columns`).
3. Run the command via Bash and present the markdown output to the user.
4. For multi-step exploration (e.g., list tables → inspect columns → run query), chain commands as needed.

## Error handling

- If the tool returns an error, present the error message to the user and suggest checking the connection string or database type.
- If a table or column is not found, inform the user and suggest running `tables` or `columns` to discover available names.