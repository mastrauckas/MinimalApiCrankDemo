namespace MinimalApiPerformanceDemo.Tools;

internal static class BenchmarkCommands
{
    public static async Task ShowMenuAsync(ToolContext context)
    {
        var choices = new[]
        {
            ("Crank — 01 Inefficient", "crank", "01-inefficient"),
            ("Crank — 02 Efficient (not implemented)", "crank", "02-efficient"),
            ("Siege — 01 Inefficient", "siege", "01-inefficient"),
            ("Siege — 02 Efficient (not implemented)", "siege", "02-efficient"),
            ("k6 — 01 Inefficient (not implemented)", "k6", "01-inefficient"),
            ("k6 — 02 Efficient (not implemented)", "k6", "02-efficient")
        };

        Console.WriteLine("Choose a benchmark:");
        for (var index = 0; index < choices.Length; index++)
        {
            Console.WriteLine($"{index + 1}. {choices[index].Item1}");
        }
        Console.Write("> ");
        if (!int.TryParse(Console.ReadLine(), out var selection) ||
            selection is < 1 or > 6)
        {
            throw new InvalidOperationException("Choose a number from 1 through 6.");
        }

        var choice = choices[selection - 1];
        if (!Directory.Exists(context.VariantRoot(choice.Item3)) || choice.Item2 == "k6")
        {
            throw new InvalidOperationException($"{choice.Item1} is not available yet.");
        }

        await RunAsync(context, choice.Item2, choice.Item3, setup: true);
    }

    public static async Task RunFromArgumentsAsync(ToolContext context, string[] arguments)
    {
        var tool = ReadOption(arguments, "--tool") ?? throw new InvalidOperationException("--tool is required.");
        var variant = ReadOption(arguments, "--variant") ?? "01-inefficient";
        var setup = arguments.Contains("--setup", StringComparer.OrdinalIgnoreCase);
        await RunAsync(context, tool, variant, setup);
    }

    private static async Task RunAsync(ToolContext context, string tool, string variant, bool setup)
    {
        if (tool is not ("crank" or "siege"))
        {
            throw new InvalidOperationException("Available tools are crank and siege.");
        }
        if (!Directory.Exists(context.VariantRoot(variant)))
        {
            throw new InvalidOperationException($"Implementation '{variant}' does not exist.");
        }

        if (setup)
        {
            await DatabaseCommands.PrepareBenchmarkAsync(context, variant);
        }

        var settings = await ReadBenchmarkSettingsAsync(context);
        var api = await StartApiAsync(context, variant, settings);
        try
        {
            var token = await LoginAsync();
            if (tool == "siege")
            {
                await RunSiegeAsync(context, token);
            }
            else
            {
                await RunCrankAsync(context, token);
            }
        }
        finally
        {
            if (!api.HasExited)
            {
                api.Kill(entireProcessTree: true);
                await api.WaitForExitAsync();
            }
            api.Dispose();
        }
    }

    private static async Task<Process> StartApiAsync(
        ToolContext context,
        string variant,
        IReadOnlyDictionary<string, string> settings)
    {
        var project = Path.Combine(context.VariantRoot(variant), "src", "MinimalApiPerformanceDemo.Api");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = context.RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add("http://127.0.0.1:8640");
        foreach (var (key, value) in settings)
        {
            startInfo.Environment[key] = value;
        }
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the API.");
        _ = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                using var response = await client.GetAsync("http://127.0.0.1:8640/health/live");
                if (response.IsSuccessStatusCode) return process;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException) { }
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        process.Kill(entireProcessTree: true);
        throw new InvalidOperationException("The API was not healthy within 60 seconds.");
    }

    private static async Task<string> LoginAsync()
    {
        using var client = new HttpClient();
        using var response = await client.PostAsJsonAsync("http://127.0.0.1:8640/api/auth/login", new { email = "demo@example.com", password = "DemoPassword123!" });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token?.AccessToken ?? throw new InvalidOperationException("Login did not return an access token.");
    }

    private static async Task RunSiegeAsync(ToolContext context, string token)
    {
        var image = "minimal-api-performance-demo-siege:local";
        var dockerfile = Path.Combine(context.RepositoryRoot, "benchmarks", "siege");
        await ProcessRunner.RunAsync(context.Runtime, ["build", "--tag", image, dockerfile], context.RepositoryRoot);
        var host = context.Runtime == "podman"
            ? NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.Name.StartsWith("vEthernet (WSL", StringComparison.OrdinalIgnoreCase))
                .Select(network => network.GetIPProperties())
                .SelectMany(properties => properties.UnicastAddresses)
                .FirstOrDefault(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString()
            : "host.docker.internal";
        if (string.IsNullOrWhiteSpace(host)) throw new InvalidOperationException("Could not locate the Podman WSL gateway.");
        await RunSiegeDurationAsync(context, image, host, token, 5, false);
        await RunSiegeDurationAsync(context, image, host, token, 15, true);
    }

    private static async Task RunSiegeDurationAsync(ToolContext context, string image, string host, string token, int seconds, bool save)
    {
        var output = await ProcessRunner.RunAsync(context.Runtime, ["run", "--rm", image, "--benchmark", "--concurrent=32", $"--time={seconds}S", "--header", $"Authorization: Bearer {token}", $"http://{host}:8640/api/products"], context.RepositoryRoot);
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            output,
            "\\\"successful_transactions\\\"\\s*:\\s*([1-9]\\d*)") ||
            System.Text.RegularExpressions.Regex.IsMatch(
                output,
                "\\\"failed_transactions\\\"\\s*:\\s*([1-9]\\d*)"))
        {
            throw new InvalidOperationException("Siege did not produce a successful, error-free measurement.");
        }
        if (save)
        {
            var directory = Path.Combine(context.RepositoryRoot, "artifacts", "benchmarks", "siege");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"products-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");
            await File.WriteAllTextAsync(path, output);
            Console.WriteLine(output);
            Console.WriteLine($"Siege results: {path}");
        }
    }

    private static async Task RunCrankAsync(ToolContext context, string token)
    {
        await EnsureDotnetToolAsync(context, "Microsoft.Crank.Controller");
        await EnsureDotnetToolAsync(context, "Microsoft.Crank.Agent");
        var directory = Path.Combine(context.RepositoryRoot, "artifacts", "benchmarks", "crank");
        Directory.CreateDirectory(directory);
        var result = Path.Combine(directory, $"products-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        using var agent = Process.Start(new ProcessStartInfo("crank-agent", "--url http://localhost:5010") { UseShellExecute = false }) ?? throw new InvalidOperationException("Could not start crank-agent.");
        await Task.Delay(TimeSpan.FromSeconds(2));
        try
        {
            await ProcessRunner.RunAsync("crank", ["--config", Path.Combine(context.RepositoryRoot, "benchmarks", "crank", "crank.yml"), "--scenario", "products", "--profile", "local", "--variable", $"bearerToken={token}", "--no-metadata", "--json", result], context.RepositoryRoot);
            await PrintCrankSummaryAsync(result);
        }
        finally { if (!agent.HasExited) agent.Kill(entireProcessTree: true); }
    }

    private static async Task PrintCrankSummaryAsync(string resultPath)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(resultPath));
        var results = document.RootElement
            .GetProperty("jobResults")
            .GetProperty("jobs")
            .GetProperty("load")
            .GetProperty("results");

        static double Metric(JsonElement results, string name) =>
            results.TryGetProperty(name, out var value) ? value.GetDouble() : double.NaN;

        Console.WriteLine();
        Console.WriteLine("Crank results");
        Console.WriteLine($"  Requests:       {Metric(results, "http/requests"):N0}");
        Console.WriteLine($"  Bad responses:  {Metric(results, "http/requests/badresponses"):N0}");
        Console.WriteLine($"  Requests/sec:   {Metric(results, "http/rps/mean"):N2}");
        Console.WriteLine($"  Latency p50:    {Metric(results, "http/latency/50"):N2} ms");
        Console.WriteLine($"  Latency p95:    {Metric(results, "http/latency/95"):N2} ms");
        Console.WriteLine($"  Latency p99:    {Metric(results, "http/latency/99"):N2} ms");
        Console.WriteLine($"  Max latency:    {Metric(results, "http/latency/max"):N2} ms");
        Console.WriteLine($"  Results file:   {resultPath}");
    }

    private static async Task EnsureDotnetToolAsync(ToolContext context, string package)
    {
        var installed = await ProcessRunner.RunAsync("dotnet", ["tool", "list", "--global"], context.RepositoryRoot);
        var action = installed.Contains(package, StringComparison.OrdinalIgnoreCase) ? "update" : "install";
        await ProcessRunner.RunAsync("dotnet", ["tool", action, package, "--global", "--version", "0.2.0-*"], context.RepositoryRoot);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadBenchmarkSettingsAsync(ToolContext context)
    {
        var inspect = await ProcessRunner.RunAsync(context.Runtime, ["inspect", "performancedemo-benchmark-sqlserver"], context.RepositoryRoot);
        using var document = System.Text.Json.JsonDocument.Parse(inspect);
        var password = document.RootElement[0].GetProperty("Config").GetProperty("Env").EnumerateArray().Select(element => element.GetString()).First(value => value!.StartsWith("MSSQL_SA_PASSWORD=", StringComparison.Ordinal))!["MSSQL_SA_PASSWORD=".Length..];
        return new Dictionary<string, string> { ["ConnectionStrings__PerformanceDemo"] = $"Server=127.0.0.1,14334;Database=PerformanceDemoBenchmark;User ID=sa;Password={password};TrustServerCertificate=True" };
    }

    private static string? ReadOption(string[] arguments, string name)
    {
        var index = Array.FindIndex(arguments, argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < arguments.Length - 1 ? arguments[index + 1] : null;
    }

    private sealed record TokenResponse(string AccessToken);
}
