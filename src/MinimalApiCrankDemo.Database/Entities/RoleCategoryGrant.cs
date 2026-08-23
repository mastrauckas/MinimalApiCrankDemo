namespace MinimalApiCrankDemo.Database.Entities;

public sealed class RoleCategoryGrant
{
    public int Id { get; init; }

    public int RoleId { get; init; }

    public int ProductCategoryId { get; init; }
}
