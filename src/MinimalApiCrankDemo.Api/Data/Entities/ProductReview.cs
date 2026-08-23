namespace MinimalApiCrankDemo.Api.Data.Entities;

internal sealed class ProductReview
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public int Rating { get; init; }

    public required string Comment { get; init; }
}
