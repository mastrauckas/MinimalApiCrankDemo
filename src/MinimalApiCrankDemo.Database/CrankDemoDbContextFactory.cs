namespace MinimalApiCrankDemo.Database;

public sealed class CrankDemoDbContextFactory :
    IDesignTimeDbContextFactory<CrankDemoDbContext>
{
    public CrankDemoDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__CrankDemo") ??
            throw new InvalidOperationException(
                "ConnectionStrings__CrankDemo is required.");
        var options = new DbContextOptionsBuilder<CrankDemoDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new CrankDemoDbContext(options);
    }
}
