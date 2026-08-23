var builder = WebApplication.CreateBuilder(args);
builder.ConfigureBuilder();

var app = builder.Build();

if (args.Contains("--initialize-database", StringComparer.Ordinal))
{
    await app.InitializeDatabaseAsync();
    await app.DisposeAsync();
    return;
}

app.ConfigureApp();
await app.RunAsync();

public partial class Program { }
