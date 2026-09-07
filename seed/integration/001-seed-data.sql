SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
BEGIN
    THROW 51000, 'Run EF Core migrations before seeding data.', 1;
END;

BEGIN TRANSACTION;

SET IDENTITY_INSERT dbo.AspNetRoles ON;

IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = N'CATALOGREADER')
BEGIN
    INSERT dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (1, N'CatalogReader', N'CATALOGREADER',
        N'11111111-1111-1111-1111-111111111111');
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = N'WAREHOUSEVIEWER')
BEGIN
    INSERT dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (2, N'WarehouseViewer', N'WAREHOUSEVIEWER',
        N'22222222-2222-2222-2222-222222222222');
END;

SET IDENTITY_INSERT dbo.AspNetRoles OFF;

SET IDENTITY_INSERT dbo.AspNetUsers ON;

IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetUsers
    WHERE NormalizedEmail = N'DEMO@EXAMPLE.COM')
BEGIN
    INSERT dbo.AspNetUsers (
        Id,
        UserName,
        NormalizedUserName,
        Email,
        NormalizedEmail,
        EmailConfirmed,
        PasswordHash,
        SecurityStamp,
        ConcurrencyStamp,
        PhoneNumber,
        PhoneNumberConfirmed,
        TwoFactorEnabled,
        LockoutEnd,
        LockoutEnabled,
        AccessFailedCount)
    VALUES (
        1,
        N'demo@example.com',
        N'DEMO@EXAMPLE.COM',
        N'demo@example.com',
        N'DEMO@EXAMPLE.COM',
        1,
        N'AQAAAAIAAYagAAAAEKv7Xqazl5xbd2gDzkNlvm8Ue1vcPUkgwhtxJUliyPo6Truqv7uGMkEnez19bVBYfQ==',
        N'33333333-3333-3333-3333-333333333333',
        N'44444444-4444-4444-4444-444444444444',
        NULL,
        0,
        0,
        NULL,
        1,
        0);
END;

SET IDENTITY_INSERT dbo.AspNetUsers OFF;

IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = 1 AND RoleId = 1)
BEGIN
    INSERT dbo.AspNetUserRoles (UserId, RoleId) VALUES (1, 1);
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = 1 AND RoleId = 2)
BEGIN
    INSERT dbo.AspNetUserRoles (UserId, RoleId) VALUES (1, 2);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = 1)
BEGIN
    INSERT dbo.UserProfiles (UserId, DisplayName)
    VALUES (1, N'Demo Identity User');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.UserPreferences WHERE UserId = 1)
BEGIN
    INSERT dbo.UserPreferences (
        UserId, PreferredCurrency, ProductsPerPage)
    VALUES (1, N'USD', 25);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.RecentLogins WHERE UserId = 1)
BEGIN
    INSERT dbo.RecentLogins (UserId, SucceededAtUtc, IpAddress)
    VALUES (1, DATEADD(day, -1, SYSDATETIMEOFFSET()), N'127.0.0.1');
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions
    WHERE RoleId = 1 AND Name = N'catalog.read')
BEGIN
    INSERT dbo.RolePermissions (RoleId, Name)
    VALUES (1, N'catalog.read');
END;

IF NOT EXISTS (
    SELECT 1 FROM dbo.RolePermissions
    WHERE RoleId = 2 AND Name = N'inventory.read')
BEGIN
    INSERT dbo.RolePermissions (RoleId, Name)
    VALUES (2, N'inventory.read');
END;

SET IDENTITY_INSERT dbo.ProductCategories ON;

INSERT dbo.ProductCategories (Id, Name)
SELECT seed.Id, seed.Name
FROM (VALUES
    (1, N'Keyboards'),
    (2, N'Mice'),
    (3, N'Monitors')) AS seed(Id, Name)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ProductCategories existing
    WHERE existing.Id = seed.Id OR existing.Name = seed.Name);

SET IDENTITY_INSERT dbo.ProductCategories OFF;

INSERT dbo.RoleCategoryGrants (RoleId, ProductCategoryId)
SELECT seed.RoleId, seed.ProductCategoryId
FROM (VALUES (1, 1), (1, 2), (2, 2), (2, 3)) AS seed(
    RoleId, ProductCategoryId)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.RoleCategoryGrants existing
    WHERE existing.RoleId = seed.RoleId
        AND existing.ProductCategoryId = seed.ProductCategoryId);

SET IDENTITY_INSERT dbo.Products ON;

INSERT dbo.Products (
    Id, ProductCategoryId, Sku, Name, Description, IsActive)
SELECT seed.Id,
    seed.ProductCategoryId,
    seed.Sku,
    seed.Name,
    N'Intentionally chatty demo data for ' + seed.Name + N'.',
    1
FROM (VALUES
    (1, 1, N'KEY-001', N'Compact Keyboard'),
    (2, 1, N'KEY-002', N'Mechanical Keyboard'),
    (3, 1, N'KEY-003', N'Ergonomic Keyboard'),
    (4, 1, N'KEY-004', N'Wireless Keyboard'),
    (5, 2, N'MOU-001', N'Optical Mouse'),
    (6, 2, N'MOU-002', N'Gaming Mouse'),
    (7, 2, N'MOU-003', N'Vertical Mouse'),
    (8, 2, N'MOU-004', N'Travel Mouse'),
    (9, 3, N'MON-001', N'24-inch Monitor'),
    (10, 3, N'MON-002', N'27-inch Monitor'),
    (11, 3, N'MON-003', N'Ultrawide Monitor'),
    (12, 3, N'MON-004', N'Portable Monitor')) AS seed(
        Id, ProductCategoryId, Sku, Name)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Products existing
    WHERE existing.Id = seed.Id OR existing.Sku = seed.Sku);

SET IDENTITY_INSERT dbo.Products OFF;

INSERT dbo.ProductInventories (
    ProductId, QuantityOnHand, WarehouseCode)
SELECT product.Id,
    warehouse.BaseQuantity + product.Id,
    warehouse.Code
FROM dbo.Products product
CROSS JOIN (VALUES (N'EAST', 10), (N'WEST', 20)) AS warehouse(
    Code, BaseQuantity)
WHERE product.Id BETWEEN 1 AND 12
    AND NOT EXISTS (
        SELECT 1 FROM dbo.ProductInventories existing
        WHERE existing.ProductId = product.Id
            AND existing.WarehouseCode = warehouse.Code);

INSERT dbo.ProductPrices (ProductId, Currency, Amount)
SELECT product.Id,
    price.Currency,
    price.BaseAmount + (product.Id * price.Multiplier)
FROM dbo.Products product
CROSS JOIN (VALUES
    (N'USD', 20, 15),
    (N'EUR', 18, 14)) AS price(Currency, BaseAmount, Multiplier)
WHERE product.Id BETWEEN 1 AND 12
    AND NOT EXISTS (
        SELECT 1 FROM dbo.ProductPrices existing
        WHERE existing.ProductId = product.Id
            AND existing.Currency = price.Currency);

INSERT dbo.ProductReviews (ProductId, Rating, Comment)
SELECT product.Id, review.Rating, review.Comment
FROM dbo.Products product
CROSS JOIN (VALUES
    (4, N'Solid demo product.'),
    (5, N'Useful for load testing.')) AS review(Rating, Comment)
WHERE product.Id BETWEEN 1 AND 12
    AND NOT EXISTS (
        SELECT 1 FROM dbo.ProductReviews existing
        WHERE existing.ProductId = product.Id
            AND existing.Rating = review.Rating
            AND existing.Comment = review.Comment);

INSERT dbo.RelatedProducts (ProductId, RelatedProductId)
SELECT product.Id,
    CASE WHEN product.Id = 12 THEN 1 ELSE product.Id + 1 END
FROM dbo.Products product
WHERE product.Id BETWEEN 1 AND 12
    AND NOT EXISTS (
        SELECT 1 FROM dbo.RelatedProducts existing
        WHERE existing.ProductId = product.Id
            AND existing.RelatedProductId =
                CASE WHEN product.Id = 12 THEN 1 ELSE product.Id + 1 END);

COMMIT TRANSACTION;
