namespace MinimalApiCrankDemo.Api.UnitTests.Dtos;

public sealed class ProductDtoTests
{
    [Fact]
    public void Constructor_PreservesPerformanceDemoFields()
    {
        var product = new ProductDto(
            1,
            "SKU-1",
            "Demo",
            "Description",
            "Category",
            10m,
            "USD",
            5,
            4.5,
            2);

        Assert.Equal(4.5, product.AverageRating);
        Assert.Equal(2, product.RelatedProductCount);
    }
}
