using MySql.Data.MySqlClient;

using Beginor.SharpDb;

namespace Beginor.SharpDbTest;

public sealed class MySqlMetadataTests {

    private const string ConnectionStringEnvironmentVariable = "SHARP_DB_MYSQL_CONNECTION_STRING";

    [Test]
    public async Task QueryTablesAsync_ReturnsCompositeForeignKeysWithCorrectColumnMapping() {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var metadata = new MetadataQueryService(new DbConnectionFactory());

        var markdown = await metadata.QueryTablesAsync(
            "mysql",
            database.ConnectionString,
            database.SchemaName
        );

        Assert.That(markdown, Does.Contain(
            $"| {database.SchemaName} | {database.ChildTableName} | BASE TABLE | child metadata | child_id | child_region -> {database.SchemaName}.{database.ParentTableName}(region_id); child_code -> {database.SchemaName}.{database.ParentTableName}(code) | {database.SchemaName}.{database.ParentTableName} |"
        ));
        Assert.That(markdown, Does.Contain(
            $"| {database.SchemaName} | {database.ParentTableName} | BASE TABLE | parent metadata | region_id, code | NULL | NULL |"
        ));
    }

    [Test]
    public async Task QueryColumnsAsync_ReturnsCompositeForeignKeyReferencesWithCorrectColumnMapping() {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var metadata = new MetadataQueryService(new DbConnectionFactory());

        var markdown = await metadata.QueryColumnsAsync(
            "mysql",
            database.ConnectionString,
            database.ChildTableName,
            database.SchemaName
        );

        Assert.That(markdown, Is.EqualTo(
            $"""
            | table_schema | table_name | column_name | ordinal_position | data_type | is_nullable | column_default | column_description | is_primary_key | is_foreign_key | referenced_table_schema | referenced_table_name | referenced_column_name |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | {database.SchemaName} | {database.ChildTableName} | child_id | 1 | int | NO | NULL | child identifier | YES | NO | NULL | NULL | NULL |
            | {database.SchemaName} | {database.ChildTableName} | child_region | 2 | int | NO | NULL | NULL | NO | YES | {database.SchemaName} | {database.ParentTableName} | region_id |
            | {database.SchemaName} | {database.ChildTableName} | child_code | 3 | varchar | NO | NULL | NULL | NO | YES | {database.SchemaName} | {database.ParentTableName} | code |
            | {database.SchemaName} | {database.ChildTableName} | note | 4 | text | YES | NULL | child note | NO | NO | NULL | NULL | NULL |
            """
        ));
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable {

        private MySqlTestDatabase(
            string connectionString,
            string schemaName,
            string parentTableName,
            string childTableName
        ) {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            ParentTableName = parentTableName;
            ChildTableName = childTableName;
        }

        public string ConnectionString { get; }

        public string SchemaName { get; }

        public string ParentTableName { get; }

        public string ChildTableName { get; }

        public static async Task<MySqlTestDatabase> CreateAsync() {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(connectionString)) {
                Assert.Ignore(
                    $"Set {ConnectionStringEnvironmentVariable} to run MySQL integration tests."
                );
            }

            var builder = new MySqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.Database)) {
                Assert.Fail("MySQL integration tests require a database in the connection string.");
            }

            var suffix = Guid.NewGuid().ToString("N")[..12];
            var parentTableName = $"sharp_db_parent_{suffix}";
            var childTableName = $"sharp_db_child_{suffix}";

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            await ExecuteAsync(connection, $"drop table if exists {QuoteIdentifier(childTableName)}");
            await ExecuteAsync(connection, $"drop table if exists {QuoteIdentifier(parentTableName)}");
            await ExecuteAsync(
                connection,
                $"""
                create table {QuoteIdentifier(parentTableName)} (
                    region_id int not null,
                    code varchar(32) not null,
                    name varchar(64) not null,
                    constraint {QuoteIdentifier($"pk_parent_{suffix}")} primary key (region_id, code)
                ) comment = 'parent metadata'
                """
            );
            await ExecuteAsync(
                connection,
                $"""
                create table {QuoteIdentifier(childTableName)} (
                    child_id int not null comment 'child identifier',
                    child_region int not null,
                    child_code varchar(32) not null,
                    note text null comment 'child note',
                    constraint {QuoteIdentifier($"pk_child_{suffix}")} primary key (child_id),
                    constraint {QuoteIdentifier($"fk_child_parent_{suffix}")}
                        foreign key (child_region, child_code)
                        references {QuoteIdentifier(parentTableName)} (region_id, code)
                ) comment = 'child metadata'
                """
            );

            return new MySqlTestDatabase(
                connectionString,
                builder.Database,
                parentTableName,
                childTableName
            );
        }

        public async ValueTask DisposeAsync() {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            await ExecuteAsync(connection, $"drop table if exists {QuoteIdentifier(ChildTableName)}");
            await ExecuteAsync(connection, $"drop table if exists {QuoteIdentifier(ParentTableName)}");
        }

        private static async Task ExecuteAsync(MySqlConnection connection, string sql) {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        private static string QuoteIdentifier(string identifier) =>
            $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";

    }

}
