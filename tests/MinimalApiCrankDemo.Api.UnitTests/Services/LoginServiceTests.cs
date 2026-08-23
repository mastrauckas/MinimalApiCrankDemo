namespace MinimalApiCrankDemo.Api.UnitTests.Services;

public sealed class LoginServiceTests
{
    [Fact]
    public async Task LoginAsync_DelegatesToRepository()
    {
        var repository = Substitute.For<ILoginRepository>();
        var request = new LoginRequestDto("demo@example.com", "password");
        var expected = new LoginResult(true, "Succeeded");
        repository.LoginAsync(request.Email,
                request.Password,
                "127.0.0.1",
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var service = new LoginService(repository);

        var result = await service.LoginAsync(request,
            "127.0.0.1",
            CancellationToken.None);

        Assert.Same(expected, result);
    }
}
