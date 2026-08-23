namespace MinimalApiCrankDemo.Api.Dtos;

public sealed record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Description,
    string Category,
    decimal Price,
    string Currency,
    int QuantityOnHand,
    double AverageRating,
    int RelatedProductCount);
