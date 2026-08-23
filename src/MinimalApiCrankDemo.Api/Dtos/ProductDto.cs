namespace MinimalApiCrankDemo.Api.Dtos;

internal sealed record ProductDto(
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
