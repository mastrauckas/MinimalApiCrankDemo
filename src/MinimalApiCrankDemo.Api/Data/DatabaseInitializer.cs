namespace MinimalApiCrankDemo.Api.Data;

internal static class DatabaseInitializerExtensions
{
    extension(WebApplication app)
    {
        public async Task InitializeDatabaseAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var database = scope.ServiceProvider
                .GetRequiredService<CrankDemoDbContext>();
            await database.Database.MigrateAsync();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<int>>>();

            var user = await SeedIdentityAsync(userManager, roleManager);
            await SeedDemoDataAsync(database, user);
        }
    }

    private static async Task<ApplicationUser> SeedIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        string[] roleNames = ["CatalogReader", "WarehouseViewer"];
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(
                    new IdentityRole<int>(roleName));
                EnsureSucceeded(roleResult, $"create role {roleName}");
            }
        }

        const string email = "demo@example.com";
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(
                user,
                "DemoPassword123!");
            EnsureSucceeded(createResult, "create demo Identity user");
            var roleResult = await userManager.AddToRolesAsync(user, roleNames);
            EnsureSucceeded(roleResult, "assign demo Identity roles");
        }

        return user;
    }

    private static async Task SeedDemoDataAsync(
        CrankDemoDbContext database,
        ApplicationUser user)
    {
        if (await database.Products.AnyAsync())
        {
            return;
        }

        var roles = await database.Roles
            .OrderBy(role => role.Id)
            .ToListAsync();
        database.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            DisplayName = "Demo Identity User"
        });
        database.UserPreferences.Add(new UserPreference
        {
            UserId = user.Id,
            PreferredCurrency = "USD",
            ProductsPerPage = 25
        });
        database.RecentLogins.Add(new RecentLogin
        {
            UserId = user.Id,
            SucceededAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            IpAddress = "127.0.0.1"
        });
        database.RolePermissions.AddRange(
            new RolePermission
            {
                RoleId = roles[0].Id,
                Name = "catalog.read"
            },
            new RolePermission
            {
                RoleId = roles[1].Id,
                Name = "inventory.read"
            });

        var categories = new[]
        {
            new ProductCategory { Name = "Keyboards" },
            new ProductCategory { Name = "Mice" },
            new ProductCategory { Name = "Monitors" }
        };
        database.ProductCategories.AddRange(categories);
        await database.SaveChangesAsync();

        database.RoleCategoryGrants.AddRange(
            new RoleCategoryGrant
            {
                RoleId = roles[0].Id,
                ProductCategoryId = categories[0].Id
            },
            new RoleCategoryGrant
            {
                RoleId = roles[0].Id,
                ProductCategoryId = categories[1].Id
            },
            new RoleCategoryGrant
            {
                RoleId = roles[1].Id,
                ProductCategoryId = categories[1].Id
            },
            new RoleCategoryGrant
            {
                RoleId = roles[1].Id,
                ProductCategoryId = categories[2].Id
            });

        var products = CreateProducts(categories);
        database.Products.AddRange(products);
        await database.SaveChangesAsync();

        foreach (var product in products)
        {
            database.ProductInventories.AddRange(
                new ProductInventory
                {
                    ProductId = product.Id,
                    QuantityOnHand = 10 + product.Id,
                    WarehouseCode = "EAST"
                },
                new ProductInventory
                {
                    ProductId = product.Id,
                    QuantityOnHand = 20 + product.Id,
                    WarehouseCode = "WEST"
                });
            database.ProductPrices.AddRange(
                new ProductPrice
                {
                    ProductId = product.Id,
                    Currency = "USD",
                    Amount = 20 + (product.Id * 15)
                },
                new ProductPrice
                {
                    ProductId = product.Id,
                    Currency = "EUR",
                    Amount = 18 + (product.Id * 14)
                });
            database.ProductReviews.AddRange(
                new ProductReview
                {
                    ProductId = product.Id,
                    Rating = 4,
                    Comment = "Solid demo product."
                },
                new ProductReview
                {
                    ProductId = product.Id,
                    Rating = 5,
                    Comment = "Useful for load testing."
                });
            database.RelatedProducts.Add(new RelatedProduct
            {
                ProductId = product.Id,
                RelatedProductId = products[product.Id % products.Length].Id
            });
        }

        await database.SaveChangesAsync();
    }

    private static Product[] CreateProducts(ProductCategory[] categories) =>
    [
        CreateProduct(categories[0].Id, "KEY-001", "Compact Keyboard"),
        CreateProduct(categories[0].Id, "KEY-002", "Mechanical Keyboard"),
        CreateProduct(categories[0].Id, "KEY-003", "Ergonomic Keyboard"),
        CreateProduct(categories[0].Id, "KEY-004", "Wireless Keyboard"),
        CreateProduct(categories[1].Id, "MOU-001", "Optical Mouse"),
        CreateProduct(categories[1].Id, "MOU-002", "Gaming Mouse"),
        CreateProduct(categories[1].Id, "MOU-003", "Vertical Mouse"),
        CreateProduct(categories[1].Id, "MOU-004", "Travel Mouse"),
        CreateProduct(categories[2].Id, "MON-001", "24-inch Monitor"),
        CreateProduct(categories[2].Id, "MON-002", "27-inch Monitor"),
        CreateProduct(categories[2].Id, "MON-003", "Ultrawide Monitor"),
        CreateProduct(categories[2].Id, "MON-004", "Portable Monitor")
    ];

    private static Product CreateProduct(int categoryId,
        string sku,
        string name) =>
        new()
        {
            ProductCategoryId = categoryId,
            Sku = sku,
            Name = name,
            Description = $"Intentionally chatty demo data for {name}.",
            IsActive = true
        };

    private static void EnsureSucceeded(IdentityResult result,
        string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Could not {operation}: {errors}");
        }
    }
}
