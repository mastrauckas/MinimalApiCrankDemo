# Agent Instructions

## Language and Framework

This project uses C# 14 and .NET 10. Always prefer features from these versions over older
alternatives.

## Code Style

Always use extension blocks over extension methods. Lines must never exceed 80 characters. Each
class must be in its own file. Prefer primary constructors over traditional constructors. Always use
`init` over `set` for properties. Always use `is null` and `is not null` instead of `== null` and
`!= null`.

For **record declarations**, each parameter must be on its own line with the opening parenthesis
alone on the first line:

```csharp
public record ItemDto(
    int Id,
    string Name,
    string Description);
```

For **method declarations and calls** with more than one parameter, the first parameter stays on the
same line as the method name. Each additional parameter goes on its own line:

```csharp
public Task<ItemDto?> UpdateAsync(int id,
    ItemDto item);

logger.LogExceptionInMiddleware(ex.Message,
    ex);
```

For HTTP handler return types, expand generic type arguments onto separate indented lines:

```csharp
private static async Task<
    Results<
        Ok<ItemDto>,
        NotFound>
    > UpdateItem(int id,
    UpdateItemRequest request,
    IItemService service)
```

Method chaining is fine when each call is short and reads naturally. When chains are long, break
each call onto its own line:

```csharp
var result = items
    .Where(x => x.IsActive)
    .OrderBy(x => x.Name)
    .Select(x => new ItemDto(x.Id, x.Name));
```

## Locals

Always use `var` for local variables. The editorconfig enforces
`csharp_style_var_elsewhere = true:warning`, which combined with `TreatWarningsAsErrors` makes
explicit types a build error for locals.

## Usings

Never add `using` directives at the top of individual C# files. Always add them to `GlobalUsings.cs`
instead.

## DTOs

DTOs must be records and placed in the `Dtos` directory. Never use default parameter values in
records. The multi-parameter rule applies — each property on its own line regardless of count:

```csharp
public record ItemDto(
    int Id,
    string Name,
    string Description);
```
