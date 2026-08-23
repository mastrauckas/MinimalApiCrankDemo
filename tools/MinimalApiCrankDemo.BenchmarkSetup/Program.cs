var connectionString = Environment.GetEnvironmentVariable(
    "ConnectionStrings__CrankDemo") ??
    throw new InvalidOperationException(
        "ConnectionStrings__CrankDemo is required.");

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<CrankDemoDbContext>(options =>
    options.UseSqlServer(connectionString));
services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<CrankDemoDbContext>();

await using var serviceProvider = services.BuildServiceProvider();
await DemoDataSeeder.SeedAsync(serviceProvider);
