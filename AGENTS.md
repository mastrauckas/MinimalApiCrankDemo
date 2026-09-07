# Repository Agent Instructions

## Package Management

Before changing dependency versions in any implementation, run:

```powershell
dotnet list .\01-inefficient\MinimalApiPerformanceDemo.slnx package --outdated
dotnet list .\01-inefficient\MinimalApiPerformanceDemo.slnx package --vulnerable --include-transitive
```

Resolve reported vulnerabilities and apply only upgrades compatible with the
solution target framework. Restore, build, and test after upgrades.

## Language and Framework

This project uses C# 14 and .NET 10. Prefer features from these versions.

## Code Style

Use extension blocks over extension methods. Lines must not exceed 80
characters. Each class must be in its own file. Prefer primary constructors.
Use `init` over `set`, and use `is null` or `is not null` for null checks.

For record declarations, put each parameter on its own line:

```csharp
public record ItemDto(
    int Id,
    string Name,
    string Description);
```

For method declarations and calls with more than one parameter, keep the first
parameter on the method line and put each additional parameter on its own line.

For HTTP handler return types, expand generic type arguments onto separate
indented lines. Break long method chains into one call per line.

## Locals

Use `var` for locals. The editorconfig treats explicit local types as errors.

## Usings

Do not add `using` directives to individual C# files. Add them to the relevant
`GlobalUsings.cs` file.

## DTOs

DTOs must be records in a `Dtos` directory. Do not use default parameter values
in records. Put every record property on its own line.
