namespace MinimalApiCrankDemo.Application.Services;

public sealed class ProductService(IProductRepository repository) :
    IProductService
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(int userId,
        CancellationToken cancellationToken) =>
        repository.GetProductsAsync(userId,
            cancellationToken);
}
