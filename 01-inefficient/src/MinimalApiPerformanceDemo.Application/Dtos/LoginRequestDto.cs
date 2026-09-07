namespace MinimalApiPerformanceDemo.Application.Dtos;

public sealed record LoginRequestDto(
    string Email,
    string Password);
