namespace MinimalApiPerformanceDemo.Database;

public static class DatabaseServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddPerformanceDemoDatabase(
            string connectionString)
        {
            services.AddDbContext<PerformanceDemoDbContext>(options =>
                options.UseSqlServer(connectionString));
            return services;
        }
    }
}
