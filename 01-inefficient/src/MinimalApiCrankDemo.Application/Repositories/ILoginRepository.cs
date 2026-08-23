namespace MinimalApiCrankDemo.Application.Repositories;

public interface ILoginRepository
{
    public Task<LoginResult> LoginAsync(string email,
        string password,
        string ipAddress,
        CancellationToken cancellationToken);
}
