using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

using Beginor.SharpDb;

namespace Beginor.SharpDbTest;

public sealed class QueryExecutorTests {

    [Test]
    public async Task ExecuteQueryAsync_ReturnsMarkdownTable() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        var markdown = await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            // language=none
            "select id, name, note from people order by id"
        );

        Assert.That(markdown, Is.EqualTo(
            """
            | id | name | note |
            | --- | --- | --- |
            | 1 | Ada | first |
            | 2 | Grace \| Hopper | NULL |
            """
        ));
    }

    [Test]
    public async Task ExecuteQueryAsync_ReturnsEmptyTableMessage() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        var markdown = await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            // language=none
            "select id, name from people where id < 0"
        );

        Assert.That(markdown, Is.EqualTo(
            """
            | id | name |
            | --- | --- |

            _No rows returned._
            """
        ));
    }

    [Test]
    public async Task ExecuteQueryAsync_ReturnsRowsAffectedForNonQuery() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        var result = await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            // language=none
            "update people set note = 'changed' where id = 1"
        );

        Assert.That(result, Is.EqualTo("Rows affected: 1"));
    }

    [Test]
    public async Task QueryTablesAsync_ReturnsTablesAndViews() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var metadata = database.CreateMetadataQueryService();

        var markdown = await metadata.QueryTablesAsync(database.DbType, database.ConnectionString);

        Assert.That(markdown, Is.EqualTo(
            """
            | table_schema | table_name | table_type | table_description | primary_key_columns | foreign_keys | related_objects |
            | --- | --- | --- | --- | --- | --- | --- |
            | main | active_people | VIEW | NULL | NULL | NULL | NULL |
            | main | people | BASE TABLE | NULL | id | NULL | NULL |
            | main | posts | BASE TABLE | NULL | id | person_id -> main.people(id) | main.people |
            """
        ));
    }

    [Test]
    public async Task QueryTablesAsync_FiltersUnsupportedSqliteSchemaToNoRows() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var metadata = database.CreateMetadataQueryService();

        var markdown = await metadata.QueryTablesAsync(
            database.DbType,
            database.ConnectionString,
            "other"
        );

        Assert.That(markdown, Is.EqualTo(
            """
            | table_schema | table_name | table_type | table_description | primary_key_columns | foreign_keys | related_objects |
            | --- | --- | --- | --- | --- | --- | --- |

            _No rows returned._
            """
        ));
    }

    [Test]
    public async Task QueryColumnsAsync_ReturnsTableColumns() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var metadata = database.CreateMetadataQueryService();

        var markdown = await metadata.QueryColumnsAsync(
            database.DbType,
            database.ConnectionString,
            "posts"
        );

        Assert.That(markdown, Is.EqualTo(
            """
            | table_schema | table_name | column_name | ordinal_position | data_type | is_nullable | column_default | column_description | is_primary_key | is_foreign_key | referenced_table_schema | referenced_table_name | referenced_column_name |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | main | posts | id | 1 | INTEGER | NO | NULL | NULL | YES | NO | NULL | NULL | NULL |
            | main | posts | person_id | 2 | INTEGER | NO | NULL | NULL | NO | YES | main | people | id |
            | main | posts | title | 3 | TEXT | NO | NULL | NULL | NO | NO | NULL | NULL | NULL |
            """
        ));
    }

    [Test]
    public async Task QueryColumnsAsync_TreatsSqlitePrimaryKeyAsNotNullable() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();
        var metadata = database.CreateMetadataQueryService();

        await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            // language=none
            "create table implicit_ids (id integer primary key, label text null)"
        );

        var markdown = await metadata.QueryColumnsAsync(
            database.DbType,
            database.ConnectionString,
            "implicit_ids"
        );

        Assert.That(markdown, Is.EqualTo(
            """
            | table_schema | table_name | column_name | ordinal_position | data_type | is_nullable | column_default | column_description | is_primary_key | is_foreign_key | referenced_table_schema | referenced_table_name | referenced_column_name |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | main | implicit_ids | id | 1 | INTEGER | NO | NULL | NULL | YES | NO | NULL | NULL | NULL |
            | main | implicit_ids | label | 2 | TEXT | YES | NULL | NULL | NO | NO | NULL | NULL | NULL |
            """
        ));
    }

    [Test]
    public async Task ExecuteFileAsync_CommitOnSuccess() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        var result = await executor.ExecuteFileAsync(
            database.DbType,
            database.ConnectionString,
            "insert into people (id, name, note) values (3, 'Linus', 'git')"
        );

        Assert.That(result, Does.Contain("Rows affected: 1"));

        var verify = await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            "select id, name from people where id = 3"
        );
        Assert.That(verify, Does.Contain("Linus"));
    }

    [Test]
    public async Task ExecuteFileAsync_RollbackOnError() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        var sql = """
            insert into people (id, name, note) values (3, 'Linus', 'git');
            insert into people (id, name, note) values (1, 'Duplicate', 'conflict');
            """;

        Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => executor.ExecuteFileAsync(database.DbType, database.ConnectionString, sql)
        );

        var verify = await executor.ExecuteQueryAsync(
            database.DbType,
            database.ConnectionString,
            "select id, name from people where id = 3"
        );
        Assert.That(verify, Does.Contain("No rows returned"));
    }

    [Test]
    public async Task ExecuteFileAsync_RejectsEmptySql() {
        await using var database = await InMemorySqliteDatabase.CreateAsync();
        var executor = database.CreateExecutor();

        Assert.ThrowsAsync<ArgumentException>(
            () => executor.ExecuteFileAsync(database.DbType, database.ConnectionString, "  ")
        );
    }

    [Test]
    public async Task Main_ReturnsHelpfulErrorForMissingOptionValue() {
        var (exitCode, error) = await RunProgramAsync("query", "--db-type", "--connection", "Data Source=:memory:", "--sql", "select 1");

        Assert.Multiple(() => {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("Missing value for argument: --db-type"));
        });
    }

    [Test]
    public async Task Main_ReturnsHelpfulErrorForUnknownOption() {
        var (exitCode, error) = await RunProgramAsync(
            "tables",
            "--db-type",
            "sqlite",
            "--connection",
            "Data Source=:memory:",
            "--unknown",
            "value"
        );

        Assert.Multiple(() => {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error, Does.Contain("Unknown argument: --unknown"));
        });
    }

    private sealed class InMemorySqliteDatabase : IAsyncDisposable {

        private readonly SqliteConnection connection;

        private InMemorySqliteDatabase(SqliteConnection connection) {
            this.connection = connection;
        }

        public string DbType => "sqlite";

        public string ConnectionString => connection.ConnectionString;

        public static async Task<InMemorySqliteDatabase> CreateAsync() {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                create table people (
                    id integer not null primary key,
                    name text not null,
                    note text null
                );

                create table posts (
                    id integer not null primary key,
                    person_id integer not null,
                    title text not null,
                    foreign key (person_id) references people (id)
                );

                insert into people (id, name, note) values
                    (1, 'Ada', 'first'),
                    (2, 'Grace | Hopper', null);

                insert into posts (id, person_id, title) values
                    (1, 1, 'Computing notes'),
                    (2, 2, 'Compiler notes');

                create view active_people as
                select id, name
                from people
                where note is not null;
                """;
            await command.ExecuteNonQueryAsync();

            return new InMemorySqliteDatabase(connection);
        }

        public QueryExecutor CreateExecutor() {
            return new QueryExecutor(new SharedConnectionFactory(connection));
        }

        public MetadataQueryService CreateMetadataQueryService() {
            return new MetadataQueryService(new SharedConnectionFactory(connection));
        }

        public async ValueTask DisposeAsync() {
            await connection.DisposeAsync();
        }

    }

    private sealed class SharedConnectionFactory(SqliteConnection sharedConnection) : IDbConnectionFactory {

        public System.Data.Common.DbConnection CreateConnection(DatabaseOptions options) {
            return new NonDisposingSqliteConnection(sharedConnection);
        }

    }

    private sealed class NonDisposingSqliteConnection(SqliteConnection inner) : System.Data.Common.DbConnection {

        [AllowNull]
        public override string ConnectionString {
            get => inner.ConnectionString;
            set => inner.ConnectionString = value ?? string.Empty;
        }

        public override string Database => inner.Database;

        public override string DataSource => inner.DataSource;

        public override string ServerVersion => inner.ServerVersion;

        public override System.Data.ConnectionState State => inner.State;

        public override void ChangeDatabase(string databaseName) {
            inner.ChangeDatabase(databaseName);
        }

        public override void Close() {
        }

        public override void Open() {
        }

        public override Task OpenAsync(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }

        protected override System.Data.Common.DbTransaction BeginDbTransaction(
            System.Data.IsolationLevel isolationLevel
        ) {
            return inner.BeginTransaction(isolationLevel);
        }

        protected override System.Data.Common.DbCommand CreateDbCommand() {
            return inner.CreateCommand();
        }

        protected override void Dispose(bool disposing) {
        }

        public override ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }

    }

    private static async Task<(int ExitCode, string Error)> RunProgramAsync(params string[] args) {
        var originalError = Console.Error;
        using var error = new StringWriter();

        try {
            Console.SetError(error);
            var exitCode = await Program.Main(args);
            return (exitCode, error.ToString());
        } finally {
            Console.SetError(originalError);
        }
    }

}
