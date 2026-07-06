using System.Data.Common;

namespace Beginor.SharpDbMcp;

public sealed class MetadataQueryService(
    IDbConnectionFactory connectionFactory
) {

    public Task<string> QueryTablesAsync(
        string dbType,
        string connectionString,
        string? schema = null,
        CancellationToken cancellationToken = default
    ) {
        var options = DatabaseOptions.Create(dbType, connectionString);
        var query = options.Type switch {
            "postgres" or "postgresql" => """
                with primary_keys as (
                    select key_usage.table_schema,
                           key_usage.table_name,
                           string_agg(key_usage.column_name, ', ' order by key_usage.ordinal_position) as primary_key_columns
                    from information_schema.table_constraints constraints
                    join information_schema.key_column_usage key_usage
                      on key_usage.constraint_schema = constraints.constraint_schema
                     and key_usage.constraint_name = constraints.constraint_name
                     and key_usage.table_schema = constraints.table_schema
                     and key_usage.table_name = constraints.table_name
                    where constraints.constraint_type = 'PRIMARY KEY'
                    group by key_usage.table_schema, key_usage.table_name
                ),
                foreign_keys as (
                    select key_usage.table_schema,
                           key_usage.table_name,
                           string_agg(
                               key_usage.column_name || ' -> ' ||
                               column_usage.table_schema || '.' ||
                               column_usage.table_name || '(' ||
                               column_usage.column_name || ')',
                               '; '
                               order by key_usage.ordinal_position
                           ) as foreign_keys,
                           string_agg(
                               distinct column_usage.table_schema || '.' || column_usage.table_name,
                               ', '
                           ) as related_objects
                    from information_schema.table_constraints constraints
                    join information_schema.key_column_usage key_usage
                      on key_usage.constraint_schema = constraints.constraint_schema
                     and key_usage.constraint_name = constraints.constraint_name
                     and key_usage.table_schema = constraints.table_schema
                     and key_usage.table_name = constraints.table_name
                    join information_schema.constraint_column_usage column_usage
                      on column_usage.constraint_schema = constraints.constraint_schema
                     and column_usage.constraint_name = constraints.constraint_name
                    where constraints.constraint_type = 'FOREIGN KEY'
                    group by key_usage.table_schema, key_usage.table_name
                )
                select tables.table_schema,
                       tables.table_name,
                       tables.table_type,
                       obj_description(classes.oid, 'pg_class') as table_description,
                       primary_keys.primary_key_columns,
                       foreign_keys.foreign_keys,
                       foreign_keys.related_objects
                from information_schema.tables tables
                join pg_namespace namespaces
                  on namespaces.nspname = tables.table_schema
                join pg_class classes
                  on classes.relnamespace = namespaces.oid
                 and classes.relname = tables.table_name
                left join primary_keys
                  on primary_keys.table_schema = tables.table_schema
                 and primary_keys.table_name = tables.table_name
                left join foreign_keys
                  on foreign_keys.table_schema = tables.table_schema
                 and foreign_keys.table_name = tables.table_name
                where tables.table_schema not in ('pg_catalog', 'information_schema')
                  and tables.table_type in ('BASE TABLE', 'VIEW')
                  and (@schema is null or @schema = '' or tables.table_schema = @schema)
                order by tables.table_schema, tables.table_name
                """,
            "mysql" => """
                select tables.table_schema,
                       tables.table_name,
                       tables.table_type,
                       tables.table_comment as table_description,
                       primary_keys.primary_key_columns,
                       foreign_keys.foreign_keys,
                       foreign_keys.related_objects
                from information_schema.tables tables
                left join (
                    select table_schema,
                           table_name,
                           group_concat(column_name order by ordinal_position separator ', ') as primary_key_columns
                    from information_schema.key_column_usage
                    where constraint_name = 'PRIMARY'
                    group by table_schema, table_name
                ) primary_keys
                  on primary_keys.table_schema = tables.table_schema
                 and primary_keys.table_name = tables.table_name
                left join (
                    select table_schema,
                           table_name,
                           group_concat(
                               concat(column_name, ' -> ', referenced_table_schema, '.', referenced_table_name, '(', referenced_column_name, ')')
                               order by ordinal_position
                               separator '; '
                           ) as foreign_keys,
                           group_concat(distinct concat(referenced_table_schema, '.', referenced_table_name) separator ', ') as related_objects
                    from information_schema.key_column_usage
                    where referenced_table_name is not null
                    group by table_schema, table_name
                ) foreign_keys
                  on foreign_keys.table_schema = tables.table_schema
                 and foreign_keys.table_name = tables.table_name
                where tables.table_schema not in ('information_schema', 'mysql', 'performance_schema', 'sys')
                  and (@schema is null or @schema = '' or tables.table_schema = @schema)
                  and tables.table_type in ('BASE TABLE', 'VIEW')
                order by tables.table_schema, tables.table_name
                """,
            "sqlite" => """
                select 'main' as table_schema,
                       objects.name as table_name,
                       case objects.type
                           when 'table' then 'BASE TABLE'
                           when 'view' then 'VIEW'
                       end as table_type,
                       null as table_description,
                       (
                           select group_concat(name, ', ')
                           from (
                               select table_info.name
                               from pragma_table_info(objects.name) table_info
                               where table_info.pk > 0
                               order by table_info.pk
                           )
                       ) as primary_key_columns,
                       (
                           select group_concat(
                               foreign_key."from" || ' -> main.' || foreign_key."table" || '(' || foreign_key."to" || ')',
                               '; '
                           )
                           from pragma_foreign_key_list(objects.name) foreign_key
                       ) as foreign_keys,
                       (
                           select group_concat(related_object, ', ')
                           from (
                               select distinct 'main.' || foreign_key."table" as related_object
                               from pragma_foreign_key_list(objects.name) foreign_key
                           )
                       ) as related_objects
                from sqlite_schema objects
                where objects.type in ('table', 'view')
                  and objects.name not like 'sqlite_%'
                  and (@schema is null or @schema = '' or @schema = 'main')
                order by objects.name
                """,
            _ => throw UnsupportedDatabaseType(options.Type)
        };

        return ExecuteMetadataQueryAsync(
            options,
            query,
            new Dictionary<string, object?> {
                ["schema"] = schema ?? string.Empty
            },
            cancellationToken
        );
    }

    public Task<string> QueryColumnsAsync(
        string dbType,
        string connectionString,
        string tableName,
        string? schema = null,
        CancellationToken cancellationToken = default
    ) {
        if (string.IsNullOrWhiteSpace(tableName)) {
            throw new ArgumentException("Table or view name must not be empty.", nameof(tableName));
        }

        var options = DatabaseOptions.Create(dbType, connectionString);
        var query = options.Type switch {
            "postgres" or "postgresql" => """
                with primary_key_columns as (
                    select key_usage.table_schema,
                           key_usage.table_name,
                           key_usage.column_name
                    from information_schema.table_constraints constraints
                    join information_schema.key_column_usage key_usage
                      on key_usage.constraint_schema = constraints.constraint_schema
                     and key_usage.constraint_name = constraints.constraint_name
                     and key_usage.table_schema = constraints.table_schema
                     and key_usage.table_name = constraints.table_name
                    where constraints.constraint_type = 'PRIMARY KEY'
                ),
                foreign_key_columns as (
                    select key_usage.table_schema,
                           key_usage.table_name,
                           key_usage.column_name,
                           column_usage.table_schema as referenced_table_schema,
                           column_usage.table_name as referenced_table_name,
                           column_usage.column_name as referenced_column_name
                    from information_schema.table_constraints constraints
                    join information_schema.key_column_usage key_usage
                      on key_usage.constraint_schema = constraints.constraint_schema
                     and key_usage.constraint_name = constraints.constraint_name
                     and key_usage.table_schema = constraints.table_schema
                     and key_usage.table_name = constraints.table_name
                    join information_schema.constraint_column_usage column_usage
                      on column_usage.constraint_schema = constraints.constraint_schema
                     and column_usage.constraint_name = constraints.constraint_name
                    where constraints.constraint_type = 'FOREIGN KEY'
                )
                select columns.table_schema,
                       columns.table_name,
                       columns.column_name,
                       columns.ordinal_position,
                       columns.data_type,
                       columns.is_nullable,
                       columns.column_default,
                       col_description(classes.oid, attributes.attnum) as column_description,
                       case when primary_key_columns.column_name is null then 'NO' else 'YES' end as is_primary_key,
                       case when foreign_key_columns.column_name is null then 'NO' else 'YES' end as is_foreign_key,
                       foreign_key_columns.referenced_table_schema,
                       foreign_key_columns.referenced_table_name,
                       foreign_key_columns.referenced_column_name
                from information_schema.columns columns
                join pg_namespace namespaces
                  on namespaces.nspname = columns.table_schema
                join pg_class classes
                  on classes.relnamespace = namespaces.oid
                 and classes.relname = columns.table_name
                join pg_attribute attributes
                  on attributes.attrelid = classes.oid
                 and attributes.attname = columns.column_name
                left join primary_key_columns
                  on primary_key_columns.table_schema = columns.table_schema
                 and primary_key_columns.table_name = columns.table_name
                 and primary_key_columns.column_name = columns.column_name
                left join foreign_key_columns
                  on foreign_key_columns.table_schema = columns.table_schema
                 and foreign_key_columns.table_name = columns.table_name
                 and foreign_key_columns.column_name = columns.column_name
                where columns.table_schema not in ('pg_catalog', 'information_schema')
                  and columns.table_name = @tableName
                  and (@schema is null or @schema = '' or columns.table_schema = @schema)
                order by columns.table_schema, columns.table_name, columns.ordinal_position
                """,
            "mysql" => """
                select columns.table_schema,
                       columns.table_name,
                       columns.column_name,
                       columns.ordinal_position,
                       columns.data_type,
                       columns.is_nullable,
                       columns.column_default,
                       columns.column_comment as column_description,
                       case when columns.column_key = 'PRI' then 'YES' else 'NO' end as is_primary_key,
                       case when key_usage.referenced_table_name is null then 'NO' else 'YES' end as is_foreign_key,
                       key_usage.referenced_table_schema,
                       key_usage.referenced_table_name,
                       key_usage.referenced_column_name
                from information_schema.columns columns
                left join information_schema.key_column_usage key_usage
                  on key_usage.table_schema = columns.table_schema
                 and key_usage.table_name = columns.table_name
                 and key_usage.column_name = columns.column_name
                 and key_usage.referenced_table_name is not null
                where columns.table_schema not in ('information_schema', 'mysql', 'performance_schema', 'sys')
                  and (@schema is null or @schema = '' or columns.table_schema = @schema)
                  and columns.table_name = @tableName
                order by columns.table_schema, columns.table_name, columns.ordinal_position
                """,
            "sqlite" => """
                select 'main' as table_schema,
                       @tableName as table_name,
                       table_info.name as column_name,
                       table_info.cid + 1 as ordinal_position,
                       table_info.type as data_type,
                       case
                           when table_info."notnull" = 0 then 'YES'
                           else 'NO'
                       end as is_nullable,
                       table_info.dflt_value as column_default,
                       null as column_description,
                       case when table_info.pk > 0 then 'YES' else 'NO' end as is_primary_key,
                       case when foreign_key.id is null then 'NO' else 'YES' end as is_foreign_key,
                       case when foreign_key."table" is null then null else 'main' end as referenced_table_schema,
                       foreign_key."table" as referenced_table_name,
                       foreign_key."to" as referenced_column_name
                from pragma_table_info(@tableName) table_info
                left join pragma_foreign_key_list(@tableName) foreign_key
                  on foreign_key."from" = table_info.name
                where @schema is null or @schema = '' or @schema = 'main'
                order by table_info.cid
                """,
            _ => throw UnsupportedDatabaseType(options.Type)
        };

        return ExecuteMetadataQueryAsync(
            options,
            query,
            new Dictionary<string, object?> {
                ["schema"] = schema ?? string.Empty,
                ["tableName"] = tableName
            },
            cancellationToken
        );
    }

    private async Task<string> ExecuteMetadataQueryAsync(
        DatabaseOptions options,
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken
    ) {
        await using var connection = connectionFactory.CreateConnection(options);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = options.CommandTimeoutSeconds;

        foreach (var parameter in parameters) {
            AddParameter(command, parameter.Key, parameter.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await MarkdownTableFormatter.FormatAsync(reader, cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value) {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static NotSupportedException UnsupportedDatabaseType(string dbType) {
        return new NotSupportedException(
            $"Unsupported dbType '{dbType}'. Supported values are postgres, mysql, sqlite."
        );
    }

}
