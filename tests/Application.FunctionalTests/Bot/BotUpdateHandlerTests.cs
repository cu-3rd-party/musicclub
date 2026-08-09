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
    [Ignore("временно отключен")]
    public async Task Start_WithAuthToken_ConfirmsAuthAndLinksUser()
    {
        var token = Guid.NewGuid();
        var user = await CreateUserAsync();
        await TestApp.AddAsync(new TgAuthUser { Id = token, UserId = user.Id, Success = false });

        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(777), $"/start auth_{token}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("✅ Authentication successful! You may return to the web app.");

        await using var db = Db();
        // var auth = await db.TgAuthUsers.SingleAsync(a => a.Id == token);
        // auth.Success.ShouldBeTrue();
        // auth.TgUserId.ShouldBe(777);

        var appUser = await db.Users.SingleAsync(u => u.Id == user.Id);
        appUser.TgUserId.ShouldBe(777);

        var permissionClaims = await db.UserClaims
            .Where(c => c.UserId == user.Id && c.ClaimType == PermissionClaimTypes.Permission)
            .Select(c => c.ClaimValue)
            .ToListAsync();
        permissionClaims.ShouldContain(Permissions.ParticipationEditOwn);
        permissionClaims.ShouldContain(Permissions.SongsEditOwn);
    }

    [Test]
    [Ignore("отключен пока не будет переведен на тг аутх внутри AspNetUsers")]
    public async Task Start_WithAlreadyUsedToken_Fails()
    {
        var token = Guid.NewGuid();
        var user = await CreateUserAsync();
        await TestApp.AddAsync(new TgAuthUser { Id = token, UserId = user.Id, Success = true, TgUserId = 999 });

        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(777), $"/start auth_{token}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("❌ Authentication failed or expired.");

        // await using var db = Db();
        // var auth = await db.TgAuthUsers.SingleAsync(a => a.Id == token);
        // auth.TgUserId.ShouldBe(999);
        // auth.Success.ShouldBeTrue();
    }

    [Test]
    public async Task Start_WithUnknownToken_Fails()
    {
        var bot = new FakeTelegramBotClient();
        var update = MessageUpdate(TextMessage(ChatId, BotUser(1), $"/start auth_{Guid.NewGuid()}"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("❌ Authentication failed or expired.");
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

    [Test]
    public async Task CalendarAttach_FullFlow_GuessesEmailAndSavesCalendar()
    {
        var user = await CreateUserAsync(displayName: "", tgUserId: 777);

        var bot = new FakeTelegramBotClient();
        var botUser = BotUser(777, firstName: "Test", lastName: "User");
        using var handler = new HandlerScope();

        await handler.Handler.HandleUpdateAsync(
            bot, CallbackUpdate(Callback("cq1", botUser, "calendar_attach")), WebAppUrl, CancellationToken.None);

        await using (var db = Db())
        {
            var state = await db.CalendarAttachStates.SingleAsync(s => s.TgUserId == 777);
            state.State.ShouldBe("2");
            state.PendingUserId.ShouldBe(user.Id);
            state.PendingEmail.ShouldBe("t.user@edu.centraluniversity.ru");
        }

        bot.SentMessages.Single().Text.ShouldBe("Is this your email: t.user@edu.centraluniversity.ru?");
        var keyboard = bot.SentMessages.Single().ReplyMarkup.ShouldBeOfType<InlineKeyboardMarkup>();
        keyboard.InlineKeyboard.Single().Select(b => b.CallbackData)
            .ShouldBe(new[] { "email_confirm_yes", "email_confirm_no" });

        await handler.Handler.HandleUpdateAsync(
            bot, CallbackUpdate(Callback("cq2", botUser, "email_confirm_yes")), WebAppUrl, CancellationToken.None);

        await using (var db = Db())
        {
            var appUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            appUser.Email.ShouldBe("t.user@edu.centraluniversity.ru");
            var state = await db.CalendarAttachStates.SingleAsync(s => s.TgUserId == 777);
            state.State.ShouldBe("1");
        }

        await handler.Handler.HandleUpdateAsync(
            bot, MessageUpdate(TextMessage(ChatId, botUser, "https://example.com/calendar.ics")), WebAppUrl, CancellationToken.None);

        await using (var db = Db())
        {
            var calendar = await db.Calendars.SingleAsync(c => c.UserId == user.Id);
            calendar.CalendarUrl.ShouldBe("https://example.com/calendar.ics");
            (await db.CalendarAttachStates.AnyAsync(s => s.TgUserId == 777)).ShouldBeFalse();
        }

        bot.SentMessages[^1].Text.ShouldBe("✅ Calendar attached.");
    }

    [Test]
    public async Task CalendarAttach_InvalidIcsUrl_RepliesInvalid()
    {
        var user = await CreateUserAsync(displayName: "", tgUserId: 777);
        await TestApp.AddAsync(new CalendarAttachState { TgUserId = 777, State = "1" });

        var bot = new FakeTelegramBotClient();
        var botUser = BotUser(777, firstName: "Test", lastName: "User");
        var update = MessageUpdate(TextMessage(ChatId, botUser, "https://example.com/no-ics"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe(
            "That does not look like a valid ICS URL. Please send a link ending with .ics.");
    }

    [Test]
    public async Task CalendarAttach_WithoutProfile_RepliesNotLinked()
    {
        var bot = new FakeTelegramBotClient();
        var botUser = BotUser(999, firstName: "Test", lastName: "User");
        var update = CallbackUpdate(Callback("cq1", botUser, "calendar_attach"));

        using var handler = new HandlerScope();
        await handler.Handler.HandleUpdateAsync(bot, update, WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe(
            "Please link your account in the Music Club web app first, then try again.");
        bot.AnsweredCallbacks.ShouldHaveSingleItem();
    }

    [Test]
    public async Task EmailInput_Flow_SavesTypedEmailThenCalendar()
    {
        var user = await CreateUserAsync(displayName: "X", tgUserId: 777);

        var bot = new FakeTelegramBotClient();
        var botUser = BotUser(777, firstName: null, lastName: null);
        using var handler = new HandlerScope();

        await handler.Handler.HandleUpdateAsync(
            bot, CallbackUpdate(Callback("cq1", botUser, "calendar_attach")), WebAppUrl, CancellationToken.None);

        bot.SentMessages.Single().Text.ShouldBe("Please enter your email address.");

        await handler.Handler.HandleUpdateAsync(
            bot, MessageUpdate(TextMessage(ChatId, botUser, "not-an-email")), WebAppUrl, CancellationToken.None);
        bot.SentMessages[^1].Text.ShouldBe("That does not look like a valid email address. Please try again.");

        await handler.Handler.HandleUpdateAsync(
            bot, MessageUpdate(TextMessage(ChatId, botUser, "typed@example.com")), WebAppUrl, CancellationToken.None);

        await using (var db = Db())
        {
            var appUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            appUser.Email.ShouldBe("typed@example.com");
        }

        bot.SentMessages[^2].Text.ShouldBe("✅ Email saved: typed@example.com");
        bot.SentMessages[^1].Text.ShouldBe("Send your calendar ICS URL.");

        await handler.Handler.HandleUpdateAsync(
            bot, MessageUpdate(TextMessage(ChatId, botUser, "https://example.com/me.ics")), WebAppUrl, CancellationToken.None);

        await using (var db = Db())
        {
            var calendar = await db.Calendars.SingleAsync(c => c.UserId == user.Id);
            calendar.CalendarUrl.ShouldBe("https://example.com/me.ics");
        }

        bot.SentMessages[^1].Text.ShouldBe("✅ Calendar attached.");
    }

    [Test]
    public async Task EmailConfirmNo_AsksForEmailInput()
    {
        var user = await CreateUserAsync(displayName: "", tgUserId: 777);

        var bot = new FakeTelegramBotClient();
        var botUser = BotUser(777, firstName: "Test", lastName: "User");
        using var handler = new HandlerScope();

        await handler.Handler.HandleUpdateAsync(
            bot, CallbackUpdate(Callback("cq1", botUser, "calendar_attach")), WebAppUrl, CancellationToken.None);

        await handler.Handler.HandleUpdateAsync(
            bot, CallbackUpdate(Callback("cq2", botUser, "email_confirm_no")), WebAppUrl, CancellationToken.None);

        bot.SentMessages[^1].Text.ShouldBe("Please enter your email address.");

        await using var db = Db();
        var state = await db.CalendarAttachStates.SingleAsync(s => s.TgUserId == 777);
        state.State.ShouldBe("3");
    }
}
