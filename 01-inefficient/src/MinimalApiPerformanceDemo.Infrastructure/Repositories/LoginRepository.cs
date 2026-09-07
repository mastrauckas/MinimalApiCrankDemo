namespace MinimalApiPerformanceDemo.Infrastructure.Repositories;

public sealed class LoginRepository(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    PerformanceDemoDbContext database) : ILoginRepository
{
    public async Task<LoginResult> LoginAsync(string email,
        string password,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        // Use Identity's built-in bearer-token handler. PasswordSignInAsync
        // writes the AccessTokenResponse; this is not custom token creation.
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;
        var signInResult = await signInManager.PasswordSignInAsync(
            email,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return new LoginResult(false, signInResult.ToString());
        }

        // INTENTIONALLY INEFFICIENT: Identity already loaded and validated the
        // user. Reloading it is a separate optimization target.
        var user = await userManager.FindByEmailAsync(email) ??
            throw new InvalidOperationException(
                "Signed-in user was not found.");

        // INTENTIONALLY INEFFICIENT: separate profile query instead of one
        // composed read model.
        _ = await database.UserProfiles
            .AsNoTracking()
            .SingleAsync(profile => profile.UserId == user.Id,
                cancellationToken);

        // INTENTIONALLY INEFFICIENT: load user-role links, then query every
        // role and its permissions independently (N+1 queries).
        var userRoles = await database.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .ToListAsync(cancellationToken);
        foreach (var userRole in userRoles)
        {
            _ = await database.Roles
                .AsNoTracking()
                .SingleAsync(role => role.Id == userRole.RoleId,
                    cancellationToken);
            _ = await database.RolePermissions
                .AsNoTracking()
                .Where(permission => permission.RoleId == userRole.RoleId)
                .ToListAsync(cancellationToken);
        }

        // INTENTIONALLY INEFFICIENT: preferences use another query.
        _ = await database.UserPreferences
            .AsNoTracking()
            .SingleAsync(preference => preference.UserId == user.Id,
                cancellationToken);

        // INTENTIONALLY INEFFICIENT: load recent-login entities just to
        // observe history, then issue a separate insert.
        _ = await database.RecentLogins
            .AsNoTracking()
            .Where(login => login.UserId == user.Id)
            .OrderByDescending(login => login.SucceededAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        database.RecentLogins.Add(new RecentLogin
        {
            UserId = user.Id,
            SucceededAtUtc = DateTimeOffset.UtcNow,
            IpAddress = ipAddress
        });
        await database.SaveChangesAsync(cancellationToken);

        return new LoginResult(true, signInResult.ToString());
    }
}
