namespace MinimalApiCrankDemo.Api.Configuration;

internal static class AppConfigurationExtensions
{
    extension(WebApplication app)
    {
        public void ConfigureApp()
        {
            // Must be first — rewrites HttpContext with real
            // client IP and scheme from proxy headers
            app.UseForwardedHeaders();

            app.UseMiddleware<ExceptionMiddleware>();
            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = EnrichWithClientIp;
            });

            // ORDER MATTERS: UseCors must come before
            // UseAuthentication/UseAuthorization so that CORS
            // preflight requests are handled before auth runs.
            if (app.Environment.IsDevelopment())
            {
                app.UseCors("AllowLocalDevelopment");
            }

            // ORDER MATTERS: UseAuthentication must come
            // before UseAuthorization.
            // Authentication establishes who the user is;
            // authorization uses that identity.
            app.UseAuthentication();
            app.UseAuthorization();

            // Uncomment to enable rate limiting (must also
            // enable in RegisterRateLimiting):
            app.UseRateLimiter();

            if (!app.Environment.IsProduction())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            // Health checks — map combined, liveness, and readiness probes
            app.MapHealthChecks("/health");
            app.MapHealthChecks("/health/live",
                new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("live")
                });
            app.MapHealthChecks("/health/ready",
                new HealthCheckOptions
                {
                    Predicate = check => check.Tags.Contains("ready")
                });

            // Feature endpoints
            app.ConfigureHttpRoutes();
        }
    }

    internal static void EnrichWithClientIp(
        IDiagnosticContext diagnosticContext,
        HttpContext httpContext) =>
        diagnosticContext.Set("ClientIp",
            httpContext.Connection.RemoteIpAddress);
}
