namespace MinimalApiCrankDemo.Api.Data;

internal static class DemoDataSeederExtensions
{
    private const string DemoEmail = "demo@example.com";
    private const string DemoPassword = "DemoPassword123!";

    extension(WebApplication app)
    {
        public async Task SeedDemoDataAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var database = scope.ServiceProvider
                .GetRequiredService<CrankDemoDbContext>();
            var pendingMigrations = await database.Database
                .GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                throw new InvalidOperationException(
                    "The database must be migrated before demo data is " +
                    "seeded. Run scripts/Apply-Migrations.ps1 first.");
            }

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<int>>>();
            var roles = await EnsureRolesAsync(roleManager);
            var user = await EnsureUserAsync(userManager, roles.Keys);

            await EnsureUserDataAsync(database, user.Id, roles);
            var categories = await EnsureCategoriesAsync(database);
            await EnsureRoleGrantsAsync(database, roles, categories);
            var products = await EnsureProductsAsync(database, categories);
            await EnsureProductDataAsync(database, products);
        }
    }

    private static async Task<
        Dictionary<string, IdentityRole<int>>
        > EnsureRolesAsync(RoleManager<IdentityRole<int>> roleManager)
    {
        string[] roleNames = ["CatalogReader", "WarehouseViewer"];
        var roles = new Dictionary<string, IdentityRole<int>>(
            StringComparer.Ordinal);
        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new IdentityRole<int>(roleName);
                var result = await roleManager.CreateAsync(role);
                EnsureSucceeded(result, $"create role {roleName}");
            }

            roles.Add(roleName, role);
        }

        return roles;
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        IEnumerable<string> roleNames)
    {
        var user = await userManager.FindByEmailAsync(DemoEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = DemoEmail,
                Email = DemoEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, DemoPassword);
            EnsureSucceeded(result, "create demo Identity user");
        }

        var assignedRoles = await userManager.GetRolesAsync(user);
        var missingRoles = roleNames
            .Except(assignedRoles, StringComparer.Ordinal)
            .ToArray();
        if (missingRoles.Length > 0)
        {
            var result = await userManager.AddToRolesAsync(user, missingRoles);
            EnsureSucceeded(result, "assign demo Identity roles");
        }

        return user;
    }

    private static async Task EnsureUserDataAsync(
        CrankDemoDbContext database,
        int userId,
        IReadOnlyDictionary<string, IdentityRole<int>> roles)
    {
        if (!await database.UserProfiles.AnyAsync(
            profile => profile.UserId == userId))
        {
            database.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                DisplayName = "Demo Identity User"
            });
        }

        if (!await database.UserPreferences.AnyAsync(
            preference => preference.UserId == userId))
        {
            database.UserPreferences.Add(new UserPreference
            {
                UserId = userId,
                PreferredCurrency = "USD",
                ProductsPerPage = 25
            });
        }

        if (!await database.RecentLogins.AnyAsync(
            login => login.UserId == userId))
        {
            database.RecentLogins.Add(new RecentLogin
            {
                UserId = userId,
                SucceededAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                IpAddress = "127.0.0.1"
            });
        }

        await EnsurePermissionAsync(
            database,
            roles["CatalogReader"].Id,
            "catalog.read");
        await EnsurePermissionAsync(
            database,
            roles["WarehouseViewer"].Id,
            "inventory.read");
        await database.SaveChangesAsync();
    }

    private static async Task EnsurePermissionAsync(
        CrankDemoDbContext database,
        int roleId,
        string permissionName)
    {
        if (!await database.RolePermissions.AnyAsync(permission =>
            permission.RoleId == roleId &&
            permission.Name == permissionName))
        {
            database.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                Name = permissionName
            });
        }
    }

    private static async Task<
        Dictionary<string, ProductCategory>
        > EnsureCategoriesAsync(CrankDemoDbContext database)
    {
        string[] categoryNames = ["Keyboards", "Mice", "Monitors"];
        var categories = new Dictionary<string, ProductCategory>(
            StringComparer.Ordinal);
        foreach (var categoryName in categoryNames)
        {
            var category = await database.ProductCategories
                .FirstOrDefaultAsync(candidate =>
                    candidate.Name == categoryName);
            if (category is null)
            {
                category = new ProductCategory { Name = categoryName };
                database.ProductCategories.Add(category);
                await database.SaveChangesAsync();
            }

            categories.Add(categoryName, category);
        }

        return categories;
    }

    private static async Task EnsureRoleGrantsAsync(
        CrankDemoDbContext database,
        IReadOnlyDictionary<string, IdentityRole<int>> roles,
        IReadOnlyDictionary<string, ProductCategory> categories)
    {
        await EnsureRoleGrantAsync(
            database,
            roles["CatalogReader"].Id,
            categories["Keyboards"].Id);
        await EnsureRoleGrantAsync(
            database,
            roles["CatalogReader"].Id,
            categories["Mice"].Id);
        await EnsureRoleGrantAsync(
            database,
            roles["WarehouseViewer"].Id,
            categories["Mice"].Id);
        await EnsureRoleGrantAsync(
            database,
            roles["WarehouseViewer"].Id,
            categories["Monitors"].Id);
        await database.SaveChangesAsync();
    }

    private static async Task EnsureRoleGrantAsync(
        CrankDemoDbContext database,
        int roleId,
        int categoryId)
    {
        if (!await database.RoleCategoryGrants.AnyAsync(grant =>
            grant.RoleId == roleId &&
            grant.ProductCategoryId == categoryId))
        {
            database.RoleCategoryGrants.Add(new RoleCategoryGrant
            {
                RoleId = roleId,
                ProductCategoryId = categoryId
            });
        }
    }

    private static async Task<Product[]> EnsureProductsAsync(
        CrankDemoDbContext database,
        IReadOnlyDictionary<string, ProductCategory> categories)
    {
        var candidates = CreateProducts(categories);
        var products = new List<Product>();
        foreach (var candidate in candidates)
        {
            var product = await database.Products
                .FirstOrDefaultAsync(existing =>
                    existing.Sku == candidate.Sku);
            if (product is null)
            {
                product = candidate;
                database.Products.Add(product);
                await database.SaveChangesAsync();
            }

            products.Add(product);
        }

        return [.. products];
    }

    private static Product[] CreateProducts(
        IReadOnlyDictionary<string, ProductCategory> categories) =>
    [
        CreateProduct(categories["Keyboards"].Id,
            "KEY-001", "Compact Keyboard"),
        CreateProduct(categories["Keyboards"].Id,
            "KEY-002", "Mechanical Keyboard"),
        CreateProduct(categories["Keyboards"].Id,
            "KEY-003", "Ergonomic Keyboard"),
        CreateProduct(categories["Keyboards"].Id,
            "KEY-004", "Wireless Keyboard"),
        CreateProduct(categories["Mice"].Id,
            "MOU-001", "Optical Mouse"),
        CreateProduct(categories["Mice"].Id,
            "MOU-002", "Gaming Mouse"),
        CreateProduct(categories["Mice"].Id,
            "MOU-003", "Vertical Mouse"),
        CreateProduct(categories["Mice"].Id,
            "MOU-004", "Travel Mouse"),
        CreateProduct(categories["Monitors"].Id,
            "MON-001", "24-inch Monitor"),
        CreateProduct(categories["Monitors"].Id,
            "MON-002", "27-inch Monitor"),
        CreateProduct(categories["Monitors"].Id,
            "MON-003", "Ultrawide Monitor"),
        CreateProduct(categories["Monitors"].Id,
            "MON-004", "Portable Monitor")
    ];

    private static async Task EnsureProductDataAsync(
        CrankDemoDbContext database,
        Product[] products)
    {
        foreach (var product in products)
        {
            await EnsureInventoryAsync(database, product, "EAST", 10);
            await EnsureInventoryAsync(database, product, "WEST", 20);
            await EnsurePriceAsync(database, product, "USD", 20, 15);
            await EnsurePriceAsync(database, product, "EUR", 18, 14);
            await EnsureReviewAsync(
                database, product, 4, "Solid demo product.");
            await EnsureReviewAsync(
                database, product, 5, "Useful for load testing.");

            var relatedId = products[product.Id % products.Length].Id;
            if (!await database.RelatedProducts.AnyAsync(related =>
                related.ProductId == product.Id &&
                related.RelatedProductId == relatedId))
            {
                database.RelatedProducts.Add(new RelatedProduct
                {
                    ProductId = product.Id,
                    RelatedProductId = relatedId
                });
            }
        }

        await database.SaveChangesAsync();
    }

    private static async Task EnsureInventoryAsync(
        CrankDemoDbContext database,
        Product product,
        string warehouseCode,
        int baseQuantity)
    {
        if (!await database.ProductInventories.AnyAsync(inventory =>
            inventory.ProductId == product.Id &&
            inventory.WarehouseCode == warehouseCode))
        {
            database.ProductInventories.Add(new ProductInventory
            {
                ProductId = product.Id,
                QuantityOnHand = baseQuantity + product.Id,
                WarehouseCode = warehouseCode
            });
        }
    }

    private static async Task EnsurePriceAsync(
        CrankDemoDbContext database,
        Product product,
        string currency,
        int baseAmount,
        int multiplier)
    {
        if (!await database.ProductPrices.AnyAsync(price =>
            price.ProductId == product.Id &&
            price.Currency == currency))
        {
            database.ProductPrices.Add(new ProductPrice
            {
                ProductId = product.Id,
                Currency = currency,
                Amount = baseAmount + (product.Id * multiplier)
            });
        }
    }

    private static async Task EnsureReviewAsync(
        CrankDemoDbContext database,
        Product product,
        int rating,
        string comment)
    {
        if (!await database.ProductReviews.AnyAsync(review =>
            review.ProductId == product.Id &&
            review.Rating == rating &&
            review.Comment == comment))
        {
            database.ProductReviews.Add(new ProductReview
            {
                ProductId = product.Id,
                Rating = rating,
                Comment = comment
            });
        }
    }

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
