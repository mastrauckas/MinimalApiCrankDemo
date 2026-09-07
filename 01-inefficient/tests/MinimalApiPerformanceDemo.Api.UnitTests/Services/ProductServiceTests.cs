namespace MinimalApiPerformanceDemo.Api.UnitTests.Services;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task GetProductsAsync_DelegatesToRepository()
    {
        var repository = Substitute.For<IProductRepository>();
        var expected = Array.Empty<ProductDto>();
        repository.GetProductsAsync(1,
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var service = new ProductService(repository);

        var result = await service.GetProductsAsync(1,
            CancellationToken.None);

        Assert.Same(expected, result);
    }
}
