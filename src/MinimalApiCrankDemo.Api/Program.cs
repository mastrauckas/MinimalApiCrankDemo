var builder = WebApplication.CreateBuilder(args);
builder.ConfigureBuilder();

var app = builder.Build();

if (args.Contains("--seed-demo-data", StringComparer.Ordinal))
{
    await app.SeedDemoDataAsync();
    await app.DisposeAsync();
    return;
}

app.ConfigureApp();
await app.RunAsync();

public partial class Program { }
