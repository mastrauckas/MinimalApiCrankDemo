namespace MinimalApiCrankDemo.Application.Services;

public interface IProductService
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(int userId,
        CancellationToken cancellationToken);
}
