namespace MinimalApiPerformanceDemo.Api.Middleware;

public sealed class ExceptionMiddleware(RequestDelegate next,
    ILogger<ExceptionMiddleware> logger,
    IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogExceptionInMiddleware(ex.Message,
                ex);
            await HandleExceptionAsync(context,
                ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context,
        Exception ex)
    {
        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = ex,
            ProblemDetails =
            {
                Type =
                    "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title =
                    "An error occurred while processing your request.",
                Status = StatusCodes.Status500InternalServerError
            }
        });
    }
}
