namespace MinimalApiCrankDemo.Api.Data.Entities;

public sealed class RelatedProduct
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public int RelatedProductId { get; init; }
}
