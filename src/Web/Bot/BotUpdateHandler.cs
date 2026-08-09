using System.Security.Claims;
using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Web.Bot;

public class BotUpdateHandler
{
    private static readonly Regex CommandRegex = new(
        @"^\/(?<command>[a-z0-9_]+)(?:@(?<botusername>[a-zA-Z0-9_]+))?(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    private static readonly Regex NamePartRegex = new("[^a-zA-Z]", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly BotOptions _options;
    private readonly ILogger<BotUpdateHandler> _logger;

    public BotUpdateHandler(
        IApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOptions<BotOptions> options,
        ILogger<BotUpdateHandler> logger)
    {
        _db = db;
        _userManager = userManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient bot, Update update, string webAppUrl, CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            await HandleMessageAsync(bot, message, webAppUrl, cancellationToken);
            return;
        }

        if (update.CallbackQuery is { } callback)
        {
            await HandleCallbackQueryAsync(bot, callback, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(
        ITelegramBotClient bot, Message message, string webAppUrl, CancellationToken cancellationToken)
    {
        var user = message.From;
        if (user is null || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var text = message.Text.Trim();

        var command = CommandRegex.Match(text);
        if (command.Success)
        {
            switch (command.Groups["command"].Value.ToLowerInvariant())
            {
                // может и есть получше способ для задания обработки команд, но я хз
                case "start":
                    var args = command.Groups["args"].Success
                        ? command.Groups["args"].Value.Trim()
                        : string.Empty;
                    if (args.Length > 0)
                    {
                        await HandleStartWithArgsAsync(bot, message, user, args, cancellationToken);
                    }
                    else
                    {
                        await HandleStartAsync(bot, message, user, webAppUrl, cancellationToken);
                    }
                    return;
                case "help":
                    await SendTextAsync(bot, message.Chat, BotTexts.Get("help.start", user.LanguageCode), cancellationToken);
                    return;
            }
        }
    }

    private async Task HandleStartAsync(
        ITelegramBotClient bot, Message message, User user, string webAppUrl, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received command /start without args");

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithWebApp(
                    BotTexts.Get("start.button", user.LanguageCode),
                    new WebAppInfo(webAppUrl)),
            },
        });

        await bot.SendMessage(
            message.Chat,
            BotTexts.Get("start.welcome", user.LanguageCode),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleStartWithArgsAsync(
        ITelegramBotClient bot, Message message, User user, string args, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received command start with {Args}", args);

        if (!args.StartsWith("auth_", StringComparison.Ordinal))
        {
            await SendTextAsync(bot, message.Chat, BotTexts.Get("start.invalid_param", user.LanguageCode), cancellationToken);
            return;
        }

        var rawUuid = args["auth_".Length..];
        if (!Guid.TryParse(rawUuid, out var token))
        {
            await SendTextAsync(bot, message.Chat, BotTexts.Get("start.invalid_token", user.LanguageCode), cancellationToken);
            return;
        }

        // TODO: временно все принимаем по дефолту
        await SendTextAsync(bot, message.Chat, BotTexts.Get("auth.ok", user.LanguageCode), cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient bot, CallbackQuery callback, CancellationToken cancellationToken)
    {
        if (callback.Message is null)
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }
        // kinda do nothing?
    }
    
    private static string NormalizeNamePart(string value)
    {
        return NamePartRegex.Replace(value, string.Empty).ToLowerInvariant();
    }

    private static Task SendTextAsync(
        ITelegramBotClient bot, ChatId chatId, string text, CancellationToken cancellationToken)
    {
        return bot.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }
}
