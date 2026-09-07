namespace MinimalApiPerformanceDemo.Api.IntegrationTests;

public sealed class IntegrationApiFactory : WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private const string DatabaseName = "PerformanceDemo";
    private const string DatabaseUser = "sa";
    private const string HostName = "127.0.0.1";
    private const string HostPort = "14333";

    private readonly string _containerRuntime = GetContainerRuntime();
    private readonly string _password = GeneratePassword();
    private readonly string _projectRoot = FindProjectRoot();

    public async Task InitializeAsync()
    {
        try
        {
            await RunToolAsync("prepare-integration");
        }
        catch (Exception setupException)
        {
            try
            {
                await RunToolAsync("cleanup-integration");
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Integration setup and cleanup both failed.",
                    setupException,
                    cleanupException);
            }

            throw;
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await RunToolAsync("cleanup-integration");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = CreateConnectionString();
        var settingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "appsettings.IntegrationTests.json");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration
                .AddJsonFile(settingsPath,
                    optional: false,
                    reloadOnChange: false)
                .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:PerformanceDemo"] =
                        connectionString
                }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PerformanceDemoDbContext>>();
            services.RemoveAll<PerformanceDemoDbContext>();
            services.AddPerformanceDemoDatabase(connectionString);
        });
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "tools",
                "RunPerformanceDemo.cs")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the integration-test project root.");
    }

    private static string GeneratePassword()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return $"Aa1!{Convert.ToHexString(randomBytes)}";
    }

    private static string GetContainerRuntime()
    {
        var containerRuntime = Environment.GetEnvironmentVariable(
            "PERFORMANCE_DEMO_CONTAINER_RUNTIME");
        return string.IsNullOrWhiteSpace(containerRuntime)
            ? "podman"
            : containerRuntime;
    }

    private string CreateConnectionString() =>
        $"Server={HostName},{HostPort};Database={DatabaseName};" +
        $"User ID={DatabaseUser};Password={_password};" +
        "TrustServerCertificate=True";

    private async Task RunToolAsync(string command)
    {
        var toolPath = Path.Combine(
            _projectRoot,
            "tools",
            "RunPerformanceDemo.cs");
        var startInfo = new ProcessStartInfo(
            "dotnet")
        {
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add(command);
        startInfo.Environment["PERFORMANCE_DEMO_CONTAINER_RUNTIME"] =
            _containerRuntime;
        startInfo.Environment["MSSQL_SA_PASSWORD"] = _password;
        startInfo.Environment["SQLSERVER_HOST"] = HostName;
        startInfo.Environment["SQLSERVER_PORT"] = HostPort;
        startInfo.Environment["SQLSERVER_DATABASE"] = DatabaseName;
        startInfo.Environment["SQLSERVER_USER"] = DatabaseUser;

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                $"Could not start {command}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{command} failed.{Environment.NewLine}" +
                $"{output}{Environment.NewLine}{error}");
        }
    }
}
