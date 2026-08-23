namespace MinimalApiCrankDemo.Database.Configurations;

public sealed class ProductPriceConfiguration :
    IEntityTypeConfiguration<ProductPrice>
{
    public void Configure(EntityTypeBuilder<ProductPrice> builder)
    {
        builder.Property(price => price.Amount)
            .HasPrecision(18, 2);
    }
}
