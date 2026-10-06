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
        builder.UseSetting("Security:Secret", "integration-tests-secret-that-is-long-enough-for-hs256");

        // Без Telegram__BotToken настоящий TelegramBotClient не создаётся — подменяем фейком
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITelegramBotClient>();
            services.AddSingleton<ITelegramBotClient, FakeTelegramBotClient>();
        });
    }
}
