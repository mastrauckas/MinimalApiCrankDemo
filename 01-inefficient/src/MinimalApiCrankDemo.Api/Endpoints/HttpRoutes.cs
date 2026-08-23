namespace MinimalApiCrankDemo.Api.Endpoints;

internal static class HttpRoutesExtensions
{
    extension(WebApplication app)
    {
        public void ConfigureHttpRoutes()
        {
            var root = app.MapGroup("api");

            app.MapDemoEndpoints(root);
        }
    }
}
