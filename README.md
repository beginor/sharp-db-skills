# 数据库 MCP 工具

一个支持多种数据库的 MCP 服务器，具有如下工具：

- `ExecuteQuery(dbType, connectionString, sql)` 执行 SQL 语句， 以 markdown 表格的形式返回结果；
- `QueryTables(dbType, connectionString, schema)` 返回数据库中的表和视图及其描述、主键、外键和关联对象信息；`schema` 为可选参数，如果数据库支持 schema 且未传入 `schema`，则返回全部 schema 下的表/视图；
- `QueryColumns(dbType, connectionString, tableName, schema)` 返回数据库表/视图中的数据列及其描述、主键、外键和关联列信息；`schema` 为可选参数，如果数据库支持 schema 且未传入 `schema`，则返回全部 schema 下匹配 `tableName` 的表/视图列；

## 命令行 stdio 模式

在命令行 stdio 模式下启动一个 MCP 实例即可。数据库连接信息由每次工具调用传入：

- `dbType` 为数据库类型，全部小写，支持 postgres、 mysql、 sqlite；
- `connectionString` 为标准的 ADO.NET 数据库连接串；

使用示例：

```sh
sharp-db-mcp
```

调用工具时传入：

```json
{
  "dbType": "postgres",
  "connectionString": "server=127.0.0.1;port=5432;database=test_db;user id=postgres;password=pgsql@18",
  "sql": "select 1 as ok"
}
```
