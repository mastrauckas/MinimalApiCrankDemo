namespace MinimalApiCrankDemo.Application.Results;

public sealed record LoginResult(
    bool Succeeded,
    string Description);
