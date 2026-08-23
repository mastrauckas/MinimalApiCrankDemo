namespace MinimalApiCrankDemo.Api.Endpoints;

internal static class DemoEndpointsExtensions
{
    extension(WebApplication app)
    {
        public void MapDemoEndpoints(RouteGroupBuilder root)
        {
            ArgumentNullException.ThrowIfNull(app);

            root.MapPost("/auth/login", Login)
                .WithName("DemoLogin")
                .WithTags("Demo authentication");

            root.MapGet("/products", GetProducts)
                .RequireAuthorization()
                .WithName("GetDemoProducts")
                .WithTags("Demo products");
        }
    }

    private static async Task<IResult> Login(LoginRequestDto request,
        ILoginService loginService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ??
            "unknown";
        var result = await loginService.LoginAsync(request,
            ipAddress,
            cancellationToken);

        return result.Succeeded
            ? TypedResults.Empty
            : TypedResults.Problem(
                result.Description,
                statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetProducts(
        ClaimsPrincipal principal,
        IProductService productService,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var products = await productService.GetProductsAsync(userId,
            cancellationToken);
        return TypedResults.Ok(products);
    }
}
