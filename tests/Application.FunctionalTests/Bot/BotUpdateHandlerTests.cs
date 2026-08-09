using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.FunctionalTests.Infrastructure;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Web.Bot;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Application.FunctionalTests.Bot;

public class BotUpdateHandlerTests : TestBase
{
    private const string WebAppUrl = "https://app.example.com";
    private const long ChatId = 111;

    private static User BotUser(long id, string? firstName = null, string? lastName = null, string? languageCode = "en")
        => new() { Id = id, FirstName = firstName ?? $"User{id}", LastName = lastName, LanguageCode = languageCode };

    private static Message TextMessage(long chatId, User from, string text)
        => new() { Chat = new Chat { Id = chatId }, From = from, Text = text };

    private static CallbackQuery Callback(string id, User from, string data, long chatId = ChatId)
        => new() { Id = id, Data = data, From = from, Message = new Message { Chat = new Chat { Id = chatId } } };

    private static Update MessageUpdate(Message message) => new() { Message = message };

    private static Update CallbackUpdate(CallbackQuery callback) => new() { CallbackQuery = callback };

    private static async Task<ApplicationUser> CreateUserAsync(
        string displayName = "Test User", long? tgUserId = null)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = $"user-{Guid.NewGuid():N}",
            DisplayName = displayName,
            TgUserId = tgUserId,
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        return user;
    }

    private sealed class HandlerScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public BotUpdateHandler Handler { get; }

        public HandlerScope()
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            Handler = _scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
        }

        public void Dispose() => _scope.Dispose();
    }

    private static ApplicationDbContext Db()
    {
        var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    [Test]
    public async Task Start_WithoutArgs_SendsWelcomeWithWebAppButton()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/start"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        var message = bot.SentMessages.ShouldHaveSingleItem();
        message.Text.ShouldBe("Welcome to Music Club! 🎸\n\nTap the button below to open the app:");
        message.ChatId!.Identifier.ShouldBe(ChatId);

        var keyboard = message.ReplyMarkup.ShouldBeOfType<InlineKeyboardMarkup>();
        var button = keyboard.InlineKeyboard.Single().Single();
        button.WebApp.ShouldNotBeNull();
        button.WebApp.Url.ShouldBe(WebAppUrl);
    }

    [Test]
    public async Task Start_WithMalformedToken_RepliesInvalidToken()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/start auth_not-a-guid"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("Invalid authentication token.");
    }

    [Test]
    public async Task Start_WithUnexpectedArgs_RepliesInvalidParam()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/start something_else"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("Invalid start parameter.");
    }

    [Test]
    public async Task Help_RepliesHelpText()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/help"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("Send /start to get the web app link.");
    }
}
