namespace MinimalApiCrankDemo.Application.Dtos;

public sealed record LoginRequestDto(
    string Email,
    string Password);
