namespace MinimalApiCrankDemo.Database.Entities;

public sealed class ProductCategory
{
    public int Id { get; init; }

    public required string Name { get; init; }
}
