namespace MinimalApiCrankDemo.Application.Services;

public interface ILoginService
{
    public Task<LoginResult> LoginAsync(LoginRequestDto request,
        string ipAddress,
        CancellationToken cancellationToken);
}
