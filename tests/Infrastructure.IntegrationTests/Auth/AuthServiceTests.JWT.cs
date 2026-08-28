namespace CuMusicClub.Infrastructure.IntegrationTests.Auth;

public partial class AuthServiceTests
{
    [Test]
    public async Task Refreshing_AccessTokens()
    {
        var (user, principal) = await CreateUserAsync("default");
        using var scope = new AuthScope();
        var access = await scope.Auth.CreateAuthSession(user, CancellationToken.None);

        var refreshSession = scope.Auth.RefreshSession(access.RefreshToken, CancellationToken.None);
        refreshSession.ShouldNotBeNull("refreshSession == null");
    }
}
