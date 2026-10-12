using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Web.Infrastructure.Oidc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Auth;

/// <summary>Вход через внешний провайдер: логика сопоставления пользователей и HTTP-обвязка без самого IdP.</summary>
public class ExternalLoginTests : TestBase
{
    private const string Auth = "/api/v1/auth";

    private static ExternalIdentity Identity(string subject,
        long? telegramUserId = null,
        string? displayName = "Анна",
        string? username = "anna")
    {
        return new ExternalIdentity("telegram", subject, displayName, username, TelegramUserId: telegramUserId);
    }

    private static async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private static Task<ApplicationUser> LoginAsync(ExternalIdentity identity)
    {
        return InScope(sp => sp.GetRequiredService<IExternalLoginService>().LoginAsync(identity));
    }

    private static Task<List<UserLogin>> LoginsOfAsync(Guid userId)
    {
        return InScope(sp => sp.GetRequiredService<ApplicationDbContext>().Set<UserLogin>().AsNoTracking()
            .Where(l => l.UserId == userId).ToListAsync());
    }

    private static HttpClient Client()
    {
        return FunctionalTestSetup.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Test]
    public async Task NewIdentity_CreatesUserWithDefaultPermissions_ThenReusesLogin()
    {
        var first = await LoginAsync(Identity("sub-1", telegramUserId: 1001));
        var second = await LoginAsync(Identity("sub-1", displayName: "Другое имя"));

        second.Id.ShouldBe(first.Id);
        first.DisplayName.ShouldBe("Анна");
        first.UserName.ShouldBe("anna");
        first.TgUserId.ShouldBe(1001);

        var logins = await LoginsOfAsync(first.Id);
        var login = logins.ShouldHaveSingleItem();
        login.Provider.ShouldBe("telegram");
        login.Subject.ShouldBe("sub-1");
        login.LastUsedAt.ShouldNotBeNull();

        var permissions = await InScope(sp =>
            sp.GetRequiredService<IApplicationUserRepository>().GetPermissionsAsync(first.Id));
        permissions.ShouldNotBeEmpty();
    }

    [Test]
    public async Task TelegramUserId_LinksAccountCreatedViaBot()
    {
        var existing = new ApplicationUser
        {
            TgUserId = 2002,
            UserName = "old_nick",
            DisplayName = "Старое имя",
        };
        await TestApp.AddAsync(existing);

        var user = await LoginAsync(Identity("sub-2", telegramUserId: 2002));

        user.Id.ShouldBe(existing.Id);
        // Заданное раньше не перезаписываем.
        user.UserName.ShouldBe("old_nick");
        user.DisplayName.ShouldBe("Старое имя");
        (await LoginsOfAsync(existing.Id)).ShouldHaveSingleItem().Subject.ShouldBe("sub-2");
    }

    [Test]
    public async Task WithoutTelegramUserId_DoesNotMatchExistingAccount()
    {
        var existing = new ApplicationUser { TgUserId = 3003, DisplayName = "Кто-то" };
        await TestApp.AddAsync(existing);

        var user = await LoginAsync(Identity("sub-3"));

        user.Id.ShouldNotBe(existing.Id);
        user.TgUserId.ShouldBeNull();
    }

    [Test]
    public async Task LinkedUserWithoutTelegramId_GetsIt_UnlessTakenByAnotherUser()
    {
        var first = await LoginAsync(Identity("sub-4"));
        var filled = await LoginAsync(Identity("sub-4", telegramUserId: 4004));
        filled.Id.ShouldBe(first.Id);
        filled.TgUserId.ShouldBe(4004);

        var other = await LoginAsync(Identity("sub-5"));
        var untouched = await LoginAsync(Identity("sub-5", telegramUserId: 4004));
        untouched.Id.ShouldBe(other.Id);
        untouched.TgUserId.ShouldBeNull();
    }

    [Test]
    public async Task LoginCode_IsExchangedForSessionOnlyOnce()
    {
        var user = await LoginAsync(Identity("sub-6"));
        var code = await InScope(sp =>
            Task.FromResult(sp.GetRequiredService<IExternalLoginService>().IssueLoginCode(user.Id)));

        using var client = Client();
        var response = await client.PostAsJsonAsync($"{Auth}/external/exchange", new { code });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<AuthSessionDto>();
        session!.User.Id.ShouldBe(user.Id);
        session.AccessToken.ShouldNotBeNullOrEmpty();
        session.RefreshToken.ShouldNotBeNullOrEmpty();

        (await client.PostAsJsonAsync($"{Auth}/external/exchange", new { code }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Exchange_WithUnknownCode_IsBadRequest()
    {
        using var client = Client();
        (await client.PostAsJsonAsync($"{Auth}/external/exchange", new { code = "nope" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Providers_ListsOnlyEnabledProviders()
    {
        using var client = Client();
        var providers = await client.GetFromJsonAsync<List<ExternalProviderDto>>($"{Auth}/providers");

        providers.ShouldBe([new ExternalProviderDto(WebApiFactory.TestOidcProvider, "Test IdP")]);
    }

    [Test]
    public async Task Challenge_RedirectsToProvider_WithShortStateAndPkce()
    {
        using var client = Client();
        var response = await client.GetAsync($"{Auth}/external/{WebApiFactory.TestOidcProvider}?returnUrl=/app/songs");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith(WebApiFactory.TestOidcAuthorizationEndpoint);
        var query = QueryHelpers.ParseQuery(new Uri(location).Query);
        query["client_id"].ToString().ShouldBe("test-client");
        query["redirect_uri"].ToString().ShouldEndWith($"/api/v1/auth/external/{WebApiFactory.TestOidcProvider}/callback");
        query["code_challenge_method"].ToString().ShouldBe("S256");
        // Telegram отвергает длинный state: свойства лежат на сервере, в state — одноразовый ключ.
        query["state"].ToString().Length.ShouldBeLessThanOrEqualTo(64);
    }

    [Test]
    public async Task Challenge_ForUnknownProvider_IsNotFound()
    {
        using var client = Client();
        (await client.GetAsync($"{Auth}/external/unknown")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Complete_WithoutExternalCookie_RedirectsToLoginWithError()
    {
        using var client = Client();
        var response = await client.GetAsync($"{Auth}/external/{WebApiFactory.TestOidcProvider}/complete");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/auth?error=external");
    }

    [Test]
    public void ToIdentity_MapsStandardClaims_AndTelegramUserIdClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "1234123412341234123"),
            new Claim("id", "987654321"),
            new Claim("name", "Анна Иванова"),
            new Claim("preferred_username", "anna"),
            new Claim("picture", "https://t.me/i/userpic/320/anna.jpg"),
        ]));
        var options = new OidcProviderOptions { TelegramUserIdClaim = "id" };

        var identity = OidcProviderRegistry.ToIdentity("telegram", options, principal);

        identity.ShouldBe(new ExternalIdentity("telegram", "1234123412341234123", "Анна Иванова", "anna",
            "https://t.me/i/userpic/320/anna.jpg", 987654321));
        OidcProviderRegistry.ToIdentity("telegram", new OidcProviderOptions(), principal)!
            .TelegramUserId.ShouldBeNull();
    }
}
