namespace MinimalApiCrankDemo.Database.Entities;

public sealed class Product
{
    public int Id { get; init; }

    public int ProductCategoryId { get; init; }

    public required string Sku { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public bool IsActive { get; init; }
}
