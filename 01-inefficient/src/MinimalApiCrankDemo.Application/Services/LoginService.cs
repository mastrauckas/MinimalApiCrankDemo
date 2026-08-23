namespace MinimalApiCrankDemo.Application.Services;

public sealed class LoginService(ILoginRepository repository) : ILoginService
{
    public Task<LoginResult> LoginAsync(LoginRequestDto request,
        string ipAddress,
        CancellationToken cancellationToken) =>
        repository.LoginAsync(request.Email,
            request.Password,
            ipAddress,
            cancellationToken);
}
