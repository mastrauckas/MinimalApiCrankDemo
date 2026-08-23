namespace MinimalApiCrankDemo.Api.IntegrationTests.Endpoints;

public sealed class DemoEndpointsTests(
    IntegrationApiFactory factory) : IClassFixture<IntegrationApiFactory>
{
    private readonly IntegrationApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SqlSeed_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider
            .GetRequiredService<CrankDemoDbContext>();

        Assert.Equal(1, await database.Users.CountAsync());
        Assert.Equal(2, await database.Roles.CountAsync());
        Assert.Equal(2, await database.UserRoles.CountAsync());
        Assert.Equal(1, await database.UserProfiles.CountAsync());
        Assert.Equal(1, await database.UserPreferences.CountAsync());
        Assert.Equal(2, await database.RolePermissions.CountAsync());
        Assert.Equal(4, await database.RoleCategoryGrants.CountAsync());
        Assert.Equal(3, await database.ProductCategories.CountAsync());
        Assert.Equal(12, await database.Products.CountAsync());
        Assert.Equal(24, await database.ProductInventories.CountAsync());
        Assert.Equal(24, await database.ProductPrices.CountAsync());
        Assert.Equal(24, await database.ProductReviews.CountAsync());
        Assert.Equal(12, await database.RelatedProducts.CountAsync());
    }

    [Fact]
    public async Task Login_WithSeededIdentityCredentials_ReturnsBearerToken()
    {
        var login = await LoginAsync();

        Assert.Equal("Bearer", login.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "demo@example.com",
                password = "wrong"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Products_WithBearerToken_ReturnsSeededProducts()
    {
        var login = await LoginAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/products");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.AccessToken);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content
            .ReadFromJsonAsync<IReadOnlyList<ProductDto>>();
        Assert.NotNull(products);
        Assert.Equal(12, products.Count);
        Assert.All(products,
            product => Assert.Equal("USD", product.Currency));
        Assert.All(products,
            product => Assert.Equal(4.5, product.AverageRating));
    }

    [Fact]
    public async Task Products_WithoutBearerToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AccessTokenResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "demo@example.com",
                password = "DemoPassword123!"
            });
        response.EnsureSuccessStatusCode();
        return await response.Content
            .ReadFromJsonAsync<AccessTokenResponse>() ??
            throw new InvalidOperationException(
                "Identity login did not return an access token response.");
    }
}
