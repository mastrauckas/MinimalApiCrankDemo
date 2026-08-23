namespace MinimalApiCrankDemo.Api.Data.Entities;

internal sealed class RecentLogin
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public DateTimeOffset SucceededAtUtc { get; init; }

    public required string IpAddress { get; init; }
}
