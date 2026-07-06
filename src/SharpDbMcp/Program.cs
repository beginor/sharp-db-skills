using System.Text;

namespace Beginor.SharpDbMcp;

public static class Program {

    public static async Task<int> Main(string[] args) {
        if (args.Length == 0 || args[0] is "--help" or "-h") {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args[1..];

        try {
            return command switch {
                "query"    => await RunQueryAsync(rest),
                "tables"   => await RunTablesAsync(rest),
                "columns"  => await RunColumnsAsync(rest),
                "--version" or "-v" => PrintVersion(),
                _          => Fail($"Unknown command '{command}'.")
            };
        } catch (Exception ex) {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintUsage() {
        var sb = new StringBuilder();
        sb.AppendLine("sharp-db — Database query CLI tool");
        sb.AppendLine();
        sb.AppendLine("Usage:");
        sb.AppendLine("  sharp-db <command> [options]");
        sb.AppendLine();
        sb.AppendLine("Commands:");
        sb.AppendLine("  query     Execute a SQL statement and return results as a markdown table");
        sb.AppendLine("  tables    List tables and views with metadata");
        sb.AppendLine("  columns   List columns for a table or view");
        sb.AppendLine();
        sb.AppendLine("Options:");
        sb.AppendLine("  --db-type <type>       Database type: postgres, mysql, sqlite (required)");
        sb.AppendLine("  --connection <string>  ADO.NET connection string (required)");
        sb.AppendLine("  --sql <statement>      SQL to execute (required for 'query')");
        sb.AppendLine("  --table <name>         Table or view name (required for 'columns')");
        sb.AppendLine("  --schema <name>        Optional schema name");
        sb.AppendLine("  --help, -h             Show this help message");
        sb.AppendLine("  --version, -v          Show version");
        Console.Out.Write(sb);
    }

    private static int PrintVersion() {
        var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        Console.Out.WriteLine($"sharp-db {version}");
        return 0;
    }

    private static int Fail(string message) {
        Console.Error.WriteLine($"Error: {message}");
        Console.Error.WriteLine("Run 'sharp-db --help' for usage information.");
        return 1;
    }

    private static async Task<int> RunQueryAsync(string[] args) {
        var dbType = Require(args, "--db-type");
        var connStr = Require(args, "--connection");
        var sql = Require(args, "--sql");

        var executor = CreateExecutor();
        var result = await executor.ExecuteQueryAsync(dbType, connStr, sql);
        Console.Out.WriteLine(result);
        return 0;
    }

    private static async Task<int> RunTablesAsync(string[] args) {
        var dbType = Require(args, "--db-type");
        var connStr = Require(args, "--connection");
        var schema = Optional(args, "--schema");

        var metadata = CreateMetadataService();
        var result = await metadata.QueryTablesAsync(dbType, connStr, schema);
        Console.Out.WriteLine(result);
        return 0;
    }

    private static async Task<int> RunColumnsAsync(string[] args) {
        var dbType = Require(args, "--db-type");
        var connStr = Require(args, "--connection");
        var tableName = Require(args, "--table");
        var schema = Optional(args, "--schema");

        var metadata = CreateMetadataService();
        var result = await metadata.QueryColumnsAsync(dbType, connStr, tableName, schema);
        Console.Out.WriteLine(result);
        return 0;
    }

    private static string Require(string[] args, string flag) {
        for (var i = 0; i < args.Length - 1; i++) {
            if (args[i] == flag) return args[i + 1];
        }
        throw new ArgumentException($"Missing required argument: {flag}");
    }

    private static string? Optional(string[] args, string flag) {
        for (var i = 0; i < args.Length - 1; i++) {
            if (args[i] == flag) return args[i + 1];
        }
        return null;
    }

    private static QueryExecutor CreateExecutor() =>
        new(new DbConnectionFactory());

    private static MetadataQueryService CreateMetadataService() =>
        new(new DbConnectionFactory());

}