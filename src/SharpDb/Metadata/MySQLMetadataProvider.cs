namespace Beginor.SharpDb.Metadata;

internal sealed class MySQLMetadataProvider(
    IDbConnectionFactory connectionFactory,
    DatabaseOptions options
)
    : BaseMetadataProvider(connectionFactory, options) {

    protected override string GetTablesQuery() {
        // language=none
        return """
            select tables.table_schema as table_schema,
                   tables.table_name as table_name,
                   tables.table_type as table_type,
                   nullif(tables.table_comment, '') as table_description,
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
            """;
    }

    protected override string GetColumnsQuery() {
        return """
            select columns.table_schema as table_schema,
                   columns.table_name as table_name,
                   columns.column_name as column_name,
                   columns.ordinal_position as ordinal_position,
                   columns.data_type as data_type,
                   columns.is_nullable as is_nullable,
                   columns.column_default as column_default,
                   nullif(columns.column_comment, '') as column_description,
                   case when columns.column_key = 'PRI' then 'YES' else 'NO' end as is_primary_key,
                   case when key_usage.referenced_table_name is null then 'NO' else 'YES' end as is_foreign_key,
                   key_usage.referenced_table_schema as referenced_table_schema,
                   key_usage.referenced_table_name as referenced_table_name,
                   key_usage.referenced_column_name as referenced_column_name
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
            """;
    }

}
