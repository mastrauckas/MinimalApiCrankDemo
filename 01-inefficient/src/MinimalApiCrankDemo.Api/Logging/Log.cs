namespace MinimalApiCrankDemo.Api.Logging;

internal static partial class Log
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Unhandled exception in request: {Message}")]
    internal static partial void LogExceptionInMiddleware(this ILogger logger,
        string message,
        Exception exception);
}
