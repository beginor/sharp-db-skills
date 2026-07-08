using System.Globalization;
using System.Data.Common;

namespace Beginor.SharpDb;

public sealed class QueryExecutor(
    IDbConnectionFactory connectionFactory
) {

    public async Task<string> ExecuteQueryAsync(
        string dbType,
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrWhiteSpace(sql)) {
            throw new ArgumentException("SQL must not be empty.", nameof(sql));
        }

        var options = DatabaseOptions.Create(dbType, connectionString);

        await using var connection = connectionFactory.CreateConnection(options);
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(connection, options, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (reader.FieldCount == 0) {
            return FormatRowsAffected(reader.RecordsAffected);
        }

        return await MarkdownTableFormatter.FormatAsync(reader, cancellationToken);
    }

    public async Task<string> ExecuteFileAsync(
        string dbType,
        string connectionString,
        string sql,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrWhiteSpace(sql)) {
            throw new ArgumentException("SQL must not be empty.", nameof(sql));
        }

        var options = DatabaseOptions.Create(dbType, connectionString);

        await using var connection = connectionFactory.CreateConnection(options);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try {
            await using var command = CreateCommand(connection, options, sql);
            command.Transaction = transaction;

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return $"SQL file executed successfully. Rows affected: {rowsAffected}";
        } catch {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static DbCommand CreateCommand(DbConnection connection, DatabaseOptions options, string sql) {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = options.CommandTimeoutSeconds;
        return command;
    }

    private static string FormatRowsAffected(int recordsAffected) {
        var value = recordsAffected < 0
            ? "unknown"
            : recordsAffected.ToString(CultureInfo.InvariantCulture);

        return $"Rows affected: {value}";
    }

}