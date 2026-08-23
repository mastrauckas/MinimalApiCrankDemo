namespace MinimalApiCrankDemo.Api.Data.Entities;

public sealed class ProductInventory
{
    public int Id { get; init; }

    public int ProductId { get; init; }

    public int QuantityOnHand { get; init; }

    public required string WarehouseCode { get; init; }
}
