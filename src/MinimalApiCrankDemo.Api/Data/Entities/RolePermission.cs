namespace MinimalApiCrankDemo.Api.Data.Entities;

internal sealed class RolePermission
{
    public int Id { get; init; }

    public int RoleId { get; init; }

    public required string Name { get; init; }
}
