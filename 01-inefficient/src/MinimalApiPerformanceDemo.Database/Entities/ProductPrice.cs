namespace MinimalApiPerformanceDemo.Database.Entities;

public sealed class ProductPrice
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public required string Currency { get; init; }

    public decimal Amount { get; init; }
}
