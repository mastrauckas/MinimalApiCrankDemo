namespace MinimalApiPerformanceDemo.Tools;

internal sealed class ToolContext
{
    private ToolContext(string repositoryRoot, string runtime)
    {
        RepositoryRoot = repositoryRoot;
        Runtime = runtime;
    }

    public string RepositoryRoot { get; }
    public string Runtime { get; }
    public string VariantRoot(string variant) => Path.Combine(RepositoryRoot, variant);
    public static string EnvironmentValue(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) ?? fallback;

    public static ToolContext Create()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "RunPerformanceDemo.cs")))
            {
                var runtime = Environment.GetEnvironmentVariable(
                    "PERFORMANCE_DEMO_CONTAINER_RUNTIME") ?? "podman";
                if (runtime is not ("podman" or "docker"))
                {
                    throw new InvalidOperationException(
                        "PERFORMANCE_DEMO_CONTAINER_RUNTIME must be podman or docker.");
                }

                return new ToolContext(directory.FullName, runtime);
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
