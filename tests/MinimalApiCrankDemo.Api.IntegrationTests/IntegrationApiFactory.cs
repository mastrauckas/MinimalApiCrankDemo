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

        await DemoDataSeeder.SeedAsync(Services);
        await DemoDataSeeder.SeedAsync(Services);
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var password = ReadPassword();
        var connectionString =
            "Server=localhost,14333;Database=CrankDemo;User ID=sa;" +
            $"Password={password};TrustServerCertificate=True";

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["ConnectionStrings:CrankDemo"] = connectionString
                }));
    }

    private string ReadPassword()
    {
        var envPath = Path.Combine(_projectRoot, ".env");
        var passwordLine = File.ReadLines(envPath)
            .FirstOrDefault(line =>
                line.StartsWith("MSSQL_SA_PASSWORD=",
                    StringComparison.Ordinal));
        return passwordLine is null
            ? throw new InvalidOperationException(
                ".env must define MSSQL_SA_PASSWORD.")
            : passwordLine["MSSQL_SA_PASSWORD=".Length..];
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
