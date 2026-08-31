namespace CuMusicClub.Infrastructure.IntegrationTests.Auth;

public partial class AuthServiceTests
{
    [Test]
    public async Task Refreshing_AccessTokens()
    {
        var (user, _) = await CreateUserAsync("default");
        using var scope = new AuthScope();
        var cancellationToken = CancellationToken.None;
        var access = await scope.Auth.CreateAuthSession(user, cancellationToken);

        var refreshSession = await scope.Auth.RefreshSession(access.RefreshToken, cancellationToken);
        refreshSession.ShouldNotBeNull("refreshSession == null");
        refreshSession.RefreshToken.ShouldNotBeNull();
        refreshSession.AccessToken.ShouldNotBeNull();
    }
}
