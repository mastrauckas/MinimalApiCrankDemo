namespace MinimalApiPerformanceDemo.Database;

public sealed class PerformanceDemoDbContextFactory :
    IDesignTimeDbContextFactory<PerformanceDemoDbContext>
{
    public PerformanceDemoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__PerformanceDemo") ??
            throw new InvalidOperationException(
                "ConnectionStrings__PerformanceDemo is required.");
        var options = new DbContextOptionsBuilder<PerformanceDemoDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new PerformanceDemoDbContext(options);
    }
}
