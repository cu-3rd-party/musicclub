using CuMusicClub.Infrastructure.IntegrationTests.Bot;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;

namespace CuMusicClub.Infrastructure.IntegrationTests.Infrastructure;

public class WebApiFactory : WebApplicationFactory<Program>
{
    public const long TestChatId = -100111;
    public const string TestBotUsername = "musicclub_test_bot";

    /// <summary>OAuth 2.0-провайдер без discovery: челлендж собирается без сети.</summary>
    public const string TestOidcProvider = "testidp";
    public const string TestOidcAuthorizationEndpoint = "https://idp.example.com/authorize";

    private readonly string _connectionString;

    public WebApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CuMusicClubDb", _connectionString);
        builder.UseSetting("Telegram:ChatId", TestChatId.ToString());
        builder.UseSetting("Telegram:BotUsername", TestBotUsername);
        builder.UseSetting("Metrics:Port", "0");
        builder.UseSetting("Security:Secret", "integration-tests-secret-that-is-long-enough-for-hs256");

        var oidc = $"Auth:Oidc:{TestOidcProvider}:";
        builder.UseSetting(oidc + "DisplayName", "Test IdP");
        builder.UseSetting(oidc + "Protocol", "oauth2");
        builder.UseSetting(oidc + "AuthorizationEndpoint", TestOidcAuthorizationEndpoint);
        builder.UseSetting(oidc + "TokenEndpoint", "https://idp.example.com/token");
        builder.UseSetting(oidc + "UserInfoEndpoint", "https://idp.example.com/userinfo");
        builder.UseSetting(oidc + "ClientId", "test-client");
        builder.UseSetting(oidc + "ClientSecret", "test-secret");
        builder.UseSetting(oidc + "ClaimKeys:sub", "id");

        // Без Telegram__BotToken настоящий TelegramBotClient не создаётся — подменяем фейком
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITelegramBotClient>();
            services.AddSingleton<ITelegramBotClient, FakeTelegramBotClient>();
        });
    }
}
