namespace MinimalApiPerformanceDemo.Infrastructure.Repositories;

public sealed class ProductRepository(PerformanceDemoDbContext database) :
    IProductRepository
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(int userId,
        CancellationToken cancellationToken)
    {
        // INTENTIONALLY INEFFICIENT: authorization already established the
        // identity, but this reloads the complete user entity.
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
        // each product below instead of projecting a composed query.
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

        return products;
    }
}
