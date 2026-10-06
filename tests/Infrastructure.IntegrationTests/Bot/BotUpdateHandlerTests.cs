using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Infrastructure.IntegrationTests.Infrastructure;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Web.Bot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Infrastructure.IntegrationTests.Bot;

public class BotUpdateHandlerTests : TestBase
{
    private const string WebAppUrl = "https://app.example.com";
    private const long ChatId = 111;

    private static User BotUser(long id, string? firstName = null, string? lastName = null, string? languageCode = "en")
    {
        return new User
        {
            Id = id,
            FirstName = firstName ?? $"User{id}",
            LastName = lastName,
            LanguageCode = languageCode,
        };
    }

    private static Message TextMessage(long chatId, User from, string text)
    {
        return new Message
        {
            Chat = new Chat
            {
                Id = chatId,
                Type = ChatType.Private,
            },
            From = from,
            Text = text,
        };
    }

    private static CallbackQuery Callback(string id, User from, string data, long chatId = ChatId)
    {
        return new CallbackQuery
        {
            Id = id,
            Data = data,
            From = from,
            Message = new Message
            {
                Chat = new Chat
                {
                    Id = chatId,
                    Type = ChatType.Private,
                },
            },
        };
    }

    private static Update MessageUpdate(Message message)
    {
        return new Update
        {
            Message = message,
        };
    }

    private static Update CallbackUpdate(CallbackQuery callback)
    {
        return new Update
        {
            CallbackQuery = callback,
        };
    }

    private static async Task<ApplicationUser> CreateUserAsync(string displayName = "Test User", long? tgUserId = null)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
        var user = new ApplicationUser
        {
            UserName = $"user-{Guid.NewGuid():N}",
            DisplayName = displayName,
            TgUserId = tgUserId,
        };
        await users.AddAsync(user);
        await users.SaveChangesAsync();

        return user;
    }

    private sealed class HandlerScope : IDisposable
    {
        private readonly IServiceScope _scope;

        public BotUpdateHandler Handler { get; }

        public IServiceProvider Services
        {
            get
            {
                return _scope.ServiceProvider;
            }
        }

        public HandlerScope()
        {
            _scope = FunctionalTestSetup.ScopeFactory.CreateScope();
            Handler = _scope.ServiceProvider.GetRequiredService<BotUpdateHandler>();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
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
        message.Text.ShouldBe(BotTexts.Welcome);
        message.ChatId!.Identifier.ShouldBe(ChatId);

        var keyboard = message.ReplyMarkup.ShouldBeOfType<InlineKeyboardMarkup>();
        var button = keyboard
            .InlineKeyboard.Single()
            .Single();
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

        bot
            .SentMessages.Single()
            .Text.ShouldBe(BotTexts.AuthInvalidToken);
    }

    [Test]
    public async Task Start_WithUnexpectedArgs_RepliesInvalidParam()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/start something_else"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot
            .SentMessages.Single()
            .Text.ShouldBe(BotTexts.StartInvalidParam);
    }

    [Test]
    public async Task Help_RepliesHelpText()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/help"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot
            .SentMessages.Single()
            .Text.ShouldBe(BotTexts.Help);
    }

    private static async Task<TgAuthLink> CreateAuthLinkAsync(DateTimeOffset? created = null)
    {
        await using var db = Db();
        var link = new TgAuthLink { Id = Guid.NewGuid() };
        db.Add(link);
        await db.SaveChangesAsync();

        if (created is { } value)
        {
            // Created проставляет интерсептор при сохранении — перезаписываем отдельным запросом
            await db.Set<TgAuthLink>()
                .Where(l => l.Id == link.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(l => l.Created, value));
        }

        return link;
    }

    private static async Task<TgAuthLink> ReloadAuthLinkAsync(Guid id)
    {
        await using var db = Db();
        return await db.Set<TgAuthLink>().AsNoTracking().SingleAsync(l => l.Id == id);
    }

    [Test]
    public async Task Start_WithValidToken_AsksForConfirmation_WithoutLinkingYet()
    {
        var link = await CreateAuthLinkAsync();
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(42), $"/start auth_{link.Id}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        var message = bot.SentMessages.ShouldHaveSingleItem();
        message.Text.ShouldBe(BotTexts.AuthConfirm);
        var buttons = message.ReplyMarkup.ShouldBeOfType<InlineKeyboardMarkup>().InlineKeyboard.Single().ToList();
        buttons.Count.ShouldBe(2);

        (await ReloadAuthLinkAsync(link.Id)).TgUserId.ShouldBeNull();
    }

    [Test]
    public async Task AuthConfirmCallback_LinksTelegramUser()
    {
        var link = await CreateAuthLinkAsync();
        var bot = new FakeTelegramBotClient();
        var update = CallbackUpdate(Callback("cb1", BotUser(42), $"auth:ok:{link.Id:N}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        (await ReloadAuthLinkAsync(link.Id)).TgUserId.ShouldBe(42);
        bot.Requests.OfType<EditMessageTextRequest>().ShouldHaveSingleItem().Text.ShouldBe(BotTexts.AuthOk);
    }

    [Test]
    public async Task AuthConfirmCallback_ForExpiredLink_DoesNotLink()
    {
        var link = await CreateAuthLinkAsync(DateTimeOffset.UtcNow - BotUpdateHandler.AuthLinkLifetime - TimeSpan.FromMinutes(1));
        var bot = new FakeTelegramBotClient();
        var update = CallbackUpdate(Callback("cb1", BotUser(42), $"auth:ok:{link.Id:N}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        (await ReloadAuthLinkAsync(link.Id)).TgUserId.ShouldBeNull();
        bot.Requests.OfType<EditMessageTextRequest>().ShouldHaveSingleItem().Text.ShouldBe(BotTexts.AuthInvalidToken);
    }

    [Test]
    public async Task Command_AddressedToAnotherBot_IsIgnored()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/help@some_other_bot"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task Command_AddressedToThisBot_IsHandled()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), $"/help@{WebApiFactory.TestBotUsername}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.ShouldHaveSingleItem().Text.ShouldBe(BotTexts.Help);
    }

    [Test]
    public async Task UnknownCommand_InPrivateChat_RepliesHint()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), "/whatever"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.ShouldHaveSingleItem().Text.ShouldBe(BotTexts.UnknownCommand);
    }

    [Test]
    public async Task Approve_WithoutPermission_IsRefused()
    {
        await CreateUserAsync(tgUserId: 77);
        var update = MessageUpdate(TextMessage(ChatId, BotUser(77), "/approve"));

        // Команды броней отвечают через ITelegramBotClient из DI (в тестах — общий фейк)
        using var handler = new HandlerScope();
        var diBot = (FakeTelegramBotClient) handler.Services.GetRequiredService<ITelegramBotClient>();
        var before = diBot.SentMessages.Count;

        await handler.Handler.HandleUpdateAsync(new FakeTelegramBotClient(), update, WebAppUrl, CancellationToken.None);

        diBot.SentMessages.Skip(before).ShouldHaveSingleItem().Text.ShouldContain("только организаторы");
    }

    [Test]
    public async Task Roadie_OutsideClubChat_IsRefused()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(5), "/roadie"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.ShouldHaveSingleItem().Text.ShouldBe(BotTexts.ClubChatOnly);
    }
}
