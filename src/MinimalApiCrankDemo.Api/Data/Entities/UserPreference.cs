namespace MinimalApiCrankDemo.Api.Data.Entities;

public sealed class UserPreference
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public required string PreferredCurrency { get; init; }

    public int ProductsPerPage { get; init; }
}
