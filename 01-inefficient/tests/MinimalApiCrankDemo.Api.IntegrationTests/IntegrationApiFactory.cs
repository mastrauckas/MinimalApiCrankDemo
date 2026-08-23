namespace MinimalApiCrankDemo.Api.IntegrationTests;

public sealed class IntegrationApiFactory : WebApplicationFactory<Program>,
    IAsyncLifetime
{
    private readonly string _projectRoot = FindProjectRoot();

    public async Task InitializeAsync()
    {
        var scriptPath = Path.Combine(
            _projectRoot,
            "scripts",
            "Prepare-IntegrationDatabase.ps1");
        var startInfo = new ProcessStartInfo(
            "pwsh",
            $"-NoProfile -File \"{scriptPath}\"")
        {
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "Could not start the integration database setup script.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await standardOutput;
        var error = await standardError;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Integration database setup failed.{Environment.NewLine}" +
                $"{output}{Environment.NewLine}{error}");
        }
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = ReadConnectionString();

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:CrankDemo"] = connectionString
                }));
    }

    private string ReadConnectionString()
    {
        var envPath = Path.Combine(_projectRoot, ".env");
        var values = File.ReadLines(envPath)
            .Where(line => !line.StartsWith('#') &&
                line.Contains('=', StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0],
                parts => parts[1],
                StringComparer.Ordinal);
        return $"Server={values["SQLSERVER_HOST"]}," +
            $"{values["SQLSERVER_PORT"]};" +
            $"Database={values["SQLSERVER_DATABASE"]};" +
            $"User ID={values["SQLSERVER_USER"]};" +
            $"Password={values["MSSQL_SA_PASSWORD"]};" +
            "TrustServerCertificate=True";
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName,
                "docker-compose.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate the project root from the test output path.");
    }
}
