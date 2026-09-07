namespace MinimalApiPerformanceDemo.Database.Entities;

public sealed class UserProfile
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public required string DisplayName { get; init; }

}
