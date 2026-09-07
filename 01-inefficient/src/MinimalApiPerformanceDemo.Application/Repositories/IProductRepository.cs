namespace MinimalApiPerformanceDemo.Application.Repositories;

public interface IProductRepository
{
    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(int userId,
        CancellationToken cancellationToken);
}
