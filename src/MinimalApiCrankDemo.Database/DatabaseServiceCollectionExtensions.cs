namespace MinimalApiCrankDemo.Database;

public static class DatabaseServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCrankDemoDatabase(
            string connectionString)
        {
            services.AddDbContext<CrankDemoDbContext>(options =>
                options.UseSqlServer(connectionString));
            return services;
        }
    }
}
