namespace MinimalApiCrankDemo.Api.Endpoints;

internal static class DemoEndpointsExtensions
{
    extension(WebApplication app)
    {
        public void MapDemoEndpoints(RouteGroupBuilder root)
        {
            ArgumentNullException.ThrowIfNull(app);

            root.MapPost("/auth/login", Login)
                .WithName("DemoLogin")
                .WithTags("Demo authentication");

            root.MapGet("/products", GetProducts)
                .RequireAuthorization()
                .WithName("GetDemoProducts")
                .WithTags("Demo products");
        }
    }

    private static async Task<IResult> Login(LoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        CrankDemoDbContext database,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Use ASP.NET Core Identity's proprietary bearer-token handler. This
        // is the same SignInManager flow used by MapIdentityApi; there is no
        // custom JWT or token-generation code in this project.
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var signInResult = await signInManager.PasswordSignInAsync(
            request.Email,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return TypedResults.Problem(
                signInResult.ToString(),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // INTENTIONALLY INEFFICIENT: Identity already loaded and validated the
        // user. Reloading it is a separate optimization target.
        var user = await userManager.FindByEmailAsync(request.Email) ??
            throw new InvalidOperationException(
                "Signed-in user was not found.");

        // INTENTIONALLY INEFFICIENT: separate profile query instead of one
        // composed read model.
        _ = await database.UserProfiles
            .AsNoTracking()
            .SingleAsync(profile => profile.UserId == user.Id,
                cancellationToken);

        // INTENTIONALLY INEFFICIENT: load user-role link rows, then query every
        // role and its permissions independently (N+1 queries).
        var userRoles = await database.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .ToListAsync(cancellationToken);
        foreach (var userRole in userRoles)
        {
            _ = await database.Roles
                .AsNoTracking()
                .SingleAsync(role => role.Id == userRole.RoleId,
                    cancellationToken);
            _ = await database.RolePermissions
                .AsNoTracking()
                .Where(permission => permission.RoleId == userRole.RoleId)
                .ToListAsync(cancellationToken);
        }

        // INTENTIONALLY INEFFICIENT: preferences are loaded in another query.
        _ = await database.UserPreferences
            .AsNoTracking()
            .SingleAsync(preference => preference.UserId == user.Id,
                cancellationToken);

        // INTENTIONALLY INEFFICIENT: load all recent-login entities just to
        // observe login history, then issue a separate insert.
        _ = await database.RecentLogins
            .AsNoTracking()
            .Where(login => login.UserId == user.Id)
            .OrderByDescending(login => login.SucceededAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        database.RecentLogins.Add(new RecentLogin
        {
            UserId = user.Id,
            SucceededAtUtc = DateTimeOffset.UtcNow,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ??
                "unknown"
        });
        await database.SaveChangesAsync(cancellationToken);

        // PasswordSignInAsync already wrote Identity's AccessTokenResponse.
        return TypedResults.Empty;
    }

    private static async Task<IResult> GetProducts(
        ClaimsPrincipal principal,
        CrankDemoDbContext database,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
        {
            return TypedResults.Unauthorized();
        }

        // INTENTIONALLY INEFFICIENT: authorization already established the
        // identity, but the endpoint reloads the complete user entity.
        var user = await database.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == userId,
                cancellationToken);

        // INTENTIONALLY INEFFICIENT: independent preference and role queries.
        var preference = await database.UserPreferences
            .AsNoTracking()
            .SingleAsync(candidate => candidate.UserId == user.Id,
                cancellationToken);
        var userRoles = await database.UserRoles
            .AsNoTracking()
            .Where(candidate => candidate.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var permittedCategoryIds = new HashSet<int>();
        foreach (var userRole in userRoles)
        {
            // INTENTIONALLY INEFFICIENT: one grant query for every role.
            var grants = await database.RoleCategoryGrants
                .AsNoTracking()
                .Where(grant => grant.RoleId == userRole.RoleId)
                .ToListAsync(cancellationToken);
            permittedCategoryIds.UnionWith(
                grants.Select(grant => grant.ProductCategoryId));
        }

        // INTENTIONALLY INEFFICIENT: load full products as an index and reload
        // each product below instead of projecting a single composed query.
        var productIndex = await database.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .ToListAsync(cancellationToken);

        var products = new List<ProductDto>();
        foreach (var indexedProduct in productIndex)
        {
            // INTENTIONALLY INEFFICIENT: redundant per-product lookup (N+1).
            var product = await database.Products
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == indexedProduct.Id,
                    cancellationToken);

            if (!permittedCategoryIds.Contains(product.ProductCategoryId))
            {
                continue;
            }

            // INTENTIONALLY INEFFICIENT: separate per-product queries for
            // category, inventory, prices, reviews, and related data.
            var category = await database.ProductCategories
                .AsNoTracking()
                .SingleAsync(candidate =>
                    candidate.Id == product.ProductCategoryId,
                    cancellationToken);
            var inventoryRows = await database.ProductInventories
                .AsNoTracking()
                .Where(candidate => candidate.ProductId == product.Id)
                .ToListAsync(cancellationToken);
            var priceRows = await database.ProductPrices
                .AsNoTracking()
                .Where(candidate => candidate.ProductId == product.Id)
                .ToListAsync(cancellationToken);
            var reviewRows = await database.ProductReviews
                .AsNoTracking()
                .Where(candidate => candidate.ProductId == product.Id)
                .ToListAsync(cancellationToken);
            var relatedRows = await database.RelatedProducts
                .AsNoTracking()
                .Where(candidate => candidate.ProductId == product.Id)
                .ToListAsync(cancellationToken);

            var price = priceRows.Single(candidate =>
                candidate.Currency == preference.PreferredCurrency);
            products.Add(new ProductDto(
                product.Id,
                product.Sku,
                product.Name,
                product.Description,
                category.Name,
                price.Amount,
                price.Currency,
                inventoryRows.Sum(row => row.QuantityOnHand),
                reviewRows.Average(review => review.Rating),
                relatedRows.Count));
        }

        return TypedResults.Ok(products);
    }
}
