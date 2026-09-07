var builder = WebApplication.CreateBuilder(args);
builder.ConfigureBuilder();

var app = builder.Build();
app.ConfigureApp();
await app.RunAsync();

public partial class Program { }
