namespace MinimalApiPerformanceDemo.Database;

public sealed class PerformanceDemoDbContext(
    DbContextOptions<PerformanceDemoDbContext> options) :
    IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RecentLogin> RecentLogins => Set<RecentLogin>();

    public DbSet<RoleCategoryGrant> RoleCategoryGrants =>
        Set<RoleCategoryGrant>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories =>
        Set<ProductCategory>();

    public DbSet<ProductInventory> ProductInventories =>
        Set<ProductInventory>();

    public DbSet<ProductPrice> ProductPrices => Set<ProductPrice>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<RelatedProduct> RelatedProducts => Set<RelatedProduct>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(PerformanceDemoDbContext).Assembly);
    }
}
