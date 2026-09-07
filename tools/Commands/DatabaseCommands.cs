namespace MinimalApiPerformanceDemo.Tools;

internal static class DatabaseCommands
{
    private const string IntegrationContainer = "performancedemo-integration-sqlserver";
    private const string BenchmarkContainer = "performancedemo-benchmark-sqlserver";

    public static async Task PrepareIntegrationAsync(ToolContext context)
    {
        var password = ToolContext.EnvironmentValue("MSSQL_SA_PASSWORD", GeneratePassword());
        var environment = Settings(password, "PerformanceDemo", "14333");
        await CleanupIntegrationAsync(context);
        await ComposeAsync(context, "docker-compose.integration-tests.yml", "up", "-d", "sqlserver", environment);
        await WaitForSqlAsync("127.0.0.1", "14333", password);
        await RecreateAsync(context, IntegrationContainer, environment);
        await MigrateAsync(context, "01-inefficient", environment);
        await ExecuteSqlAsync(context, IntegrationContainer, environment,
            "IF EXISTS (SELECT 1 FROM dbo.AspNetUsers) THROW 51000, 'Migration inserted demo data.', 1;");
        await SeedAsync(context, IntegrationContainer, environment, "integration");
        await SeedAsync(context, IntegrationContainer, environment, "integration");
    }

    public static async Task CleanupIntegrationAsync(ToolContext context)
    {
        await ComposeAsync(context, "docker-compose.integration-tests.yml", "down", "-v", "--remove-orphans");
        await IgnoreFailureAsync(context, "rm", "--force", IntegrationContainer);
        await IgnoreFailureAsync(context, "volume", "rm", "--force", "performancedemo-integration-sqlserver-data");
        await IgnoreFailureAsync(context, "network", "rm", "--force", "minimal-api-performance-demo-integration-tests_default");
    }

    public static async Task PrepareBenchmarkAsync(ToolContext context, string variant)
    {
        var password = GeneratePassword();
        var environment = Settings(password, "PerformanceDemoBenchmark", "14334");
        await CleanupBenchmarkAsync(context, environment);
        await ComposeAsync(context, "docker-compose.benchmark.yml", "up", "-d", "sqlserver", environment);
        await WaitForSqlAsync("127.0.0.1", "14334", password);
        await RecreateAsync(context, BenchmarkContainer, environment);
        await MigrateAsync(context, variant, environment);
        await SeedAsync(context, BenchmarkContainer, environment, "benchmark");
    }

    public static Task CleanupBenchmarkAsync(ToolContext context) =>
        CleanupBenchmarkAsync(context, null);

    public static async Task ResetBenchmarkDataAsync(ToolContext context, string variant)
    {
        var environment = await ReadBenchmarkEnvironmentAsync(context);
        await WaitForSqlAsync("127.0.0.1", "14334", environment["MSSQL_SA_PASSWORD"]);
        await RecreateAsync(context, BenchmarkContainer, environment);
        await MigrateAsync(context, variant, environment);
        await SeedAsync(context, BenchmarkContainer, environment, "benchmark");
    }

    public static Task MigrateAsync(ToolContext context, string variant) =>
        MigrateAsync(context, variant, Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string && entry.Value is string)
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!));

    public static Task MigrateAsync(
        ToolContext context,
        string variant,
        IReadOnlyDictionary<string, string> environment)
    {
        var databaseProject = Path.Combine(context.VariantRoot(variant), "src", "MinimalApiPerformanceDemo.Database", "MinimalApiPerformanceDemo.Database.csproj");
        return ProcessRunner.RunAsync("dotnet", ["tool", "run", "dotnet-ef", "database", "update", "--project", databaseProject, "--startup-project", databaseProject], context.VariantRoot(variant), environment);
    }

    public static async Task BuildMigrationBundleAsync(ToolContext context, string[] arguments)
    {
        var version = ReadOption(arguments, "--version") ??
            throw new InvalidOperationException("--version is required.");
        if (version.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            throw new InvalidOperationException("--version may contain only letters, numbers, periods, underscores, and hyphens.");
        }

        var variant = ReadOption(arguments, "--variant") ?? "01-inefficient";
        var project = Path.Combine(context.VariantRoot(variant), "src", "MinimalApiPerformanceDemo.Database", "MinimalApiPerformanceDemo.Database.csproj");
        var output = Path.Combine(context.RepositoryRoot, "artifacts", "migration-bundles", version, "efbundle.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var environment = new Dictionary<string, string>
        {
            ["ConnectionStrings__PerformanceDemo"] = "Server=unused;Database=PerformanceDemo;Integrated Security=True;Encrypt=True"
        };
        await ProcessRunner.RunAsync("dotnet", ["tool", "restore"], context.VariantRoot(variant), environment);
        await ProcessRunner.RunAsync("dotnet", ["tool", "run", "dotnet-ef", "migrations", "bundle", "--project", project, "--startup-project", project, "--output", output, "--force"], context.VariantRoot(variant), environment);
        Console.WriteLine(output);
    }

    public static async Task MigrateProductionAsync(ToolContext context, string[] arguments)
    {
        var connection = ReadOption(arguments, "--connection") ??
            throw new InvalidOperationException("--connection is required.");
        var bundle = ReadOption(arguments, "--bundle") ??
            throw new InvalidOperationException("--bundle is required.");
        var bundlePath = Path.GetFullPath(bundle, context.RepositoryRoot);
        if (!File.Exists(bundlePath)) throw new FileNotFoundException("Migration bundle was not found.", bundlePath);
        await ProcessRunner.RunAsync(bundlePath, ["--connection", connection], context.RepositoryRoot);
    }

    private static async Task ComposeAsync(ToolContext context, string file, params string[] arguments) =>
        await ComposeAsync(context, file, arguments, null);

    private static Task CleanupBenchmarkAsync(
        ToolContext context,
        IReadOnlyDictionary<string, string>? environment) =>
        ComposeAsync(context, "docker-compose.benchmark.yml", ["down", "-v", "--remove-orphans"], environment);

    private static Task ComposeAsync(ToolContext context, string file, string first, string second, string third, IReadOnlyDictionary<string, string> environment) =>
        ComposeAsync(context, file, [first, second, third], environment);

    private static Task ComposeAsync(ToolContext context, string file, string first, string second, string third, string fourth, IReadOnlyDictionary<string, string> environment) =>
        ComposeAsync(context, file, [first, second, third, fourth], environment);

    private static Task ComposeAsync(ToolContext context, string file, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment) =>
        ProcessRunner.RunAsync(context.Runtime, ["compose", "--file", Path.Combine(context.RepositoryRoot, file), .. arguments], context.RepositoryRoot, environment);

    private static Task IgnoreFailureAsync(ToolContext context, params string[] arguments) =>
        ProcessRunner.RunAsync(context.Runtime, arguments, context.RepositoryRoot, throwOnError: false);

    private static async Task RecreateAsync(ToolContext context, string container, IReadOnlyDictionary<string, string> environment) =>
        await ExecuteScriptAsync(context, container, environment, "-d master -i /seed/001-recreate-database.sql");

    private static async Task SeedAsync(ToolContext context, string container, IReadOnlyDictionary<string, string> environment, string kind) =>
        await ExecuteScriptAsync(context, container, environment, $"-d \"$SQLSERVER_DATABASE\" -I -i /seed/{kind}/001-seed-data.sql");

    private static async Task ExecuteSqlAsync(ToolContext context, string container, IReadOnlyDictionary<string, string> environment, string sql) =>
        await ExecuteScriptAsync(context, container, environment, $"-d \"$SQLSERVER_DATABASE\" -Q \"{sql}\"");

    private static Task ExecuteScriptAsync(ToolContext context, string container, IReadOnlyDictionary<string, string> environment, string sqlcmdArguments)
    {
        var command = "SQLCMDPASSWORD=\"$MSSQL_SA_PASSWORD\" /opt/mssql-tools18/bin/sqlcmd " +
            "-S localhost -U \"$SQLSERVER_USER\" -C -b " + sqlcmdArguments;
        return ProcessRunner.RunAsync(context.Runtime, ["exec", container, "/bin/bash", "-c", command], context.RepositoryRoot, environment);
    }

    private static async Task WaitForSqlAsync(string host, string port, string password)
    {
        var connectionString = $"Server={host},{port};Database=master;User ID=sa;Password={password};Encrypt=True;TrustServerCertificate=True;Connect Timeout=3;Pooling=False";
        var deadline = DateTime.UtcNow.AddSeconds(120);
        Exception? lastException = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception exception)
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        throw new InvalidOperationException("SQL Server was not reachable within 120 seconds.", lastException);
    }

    private static Dictionary<string, string> Settings(string password, string database, string port) => new(StringComparer.Ordinal)
    {
        ["PERFORMANCE_DEMO_CONTAINER_RUNTIME"] = Environment.GetEnvironmentVariable("PERFORMANCE_DEMO_CONTAINER_RUNTIME") ?? "podman",
        ["MSSQL_SA_PASSWORD"] = password,
        ["SQLSERVER_HOST"] = "127.0.0.1",
        ["SQLSERVER_PORT"] = port,
        ["SQLSERVER_DATABASE"] = database,
        ["SQLSERVER_USER"] = "sa",
        ["ConnectionStrings__PerformanceDemo"] = $"Server=127.0.0.1,{port};Database={database};User ID=sa;Password={password};TrustServerCertificate=True"
    };

    private static string GeneratePassword() => "Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static async Task<Dictionary<string, string>> ReadBenchmarkEnvironmentAsync(ToolContext context)
    {
        var inspect = await ProcessRunner.RunAsync(context.Runtime, ["inspect", BenchmarkContainer], context.RepositoryRoot);
        using var document = JsonDocument.Parse(inspect);
        var password = document.RootElement[0].GetProperty("Config").GetProperty("Env")
            .EnumerateArray()
            .Select(element => element.GetString())
            .FirstOrDefault(value => value?.StartsWith("MSSQL_SA_PASSWORD=", StringComparison.Ordinal) == true)?["MSSQL_SA_PASSWORD=".Length..];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("The benchmark SQL Server password could not be read.");
        }
        return Settings(password, "PerformanceDemoBenchmark", "14334");
    }

    private static string? ReadOption(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < arguments.Length - 1 ? arguments[index + 1] : null;
    }
}
