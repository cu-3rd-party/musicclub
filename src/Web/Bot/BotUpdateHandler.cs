using System.Net;
using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Web.Bot;

public class BotUpdateHandler(
    ITgAuthLinkRepository tgAuthLinks,
    IApplicationUserRepository userRepository,
    ITelegramAuthService tgAuthService,
    ITelegramChatService telegramChatService,
    IRoadieService roadieService,
    ISongTopicRepository songTopicRepository,
    ISongRepository songRepository,
    IOptions<TelegramOptions> telegramOptions,
    ILogger<BotUpdateHandler> logger)
{
    private static readonly Regex CommandRegex = new(
        @"^\/(?<command>[a-z0-9_]+)(?:@(?<botusername>[a-zA-Z0-9_]+))?(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private static readonly Regex NamePartRegex = new("[^a-zA-Z]", RegexOptions.Compiled);

    public async Task HandleUpdateAsync(ITelegramBotClient bot,
        Update update,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        if (update.Message is
            {
            } message)
        {
            await HandleMessageAsync(bot, message, webAppUrl, cancellationToken);
            return;
        }

        if (update.CallbackQuery is
            {
            } callback)
            await HandleCallbackQueryAsync(bot, callback, cancellationToken);
    }

    private async Task HandleMessageAsync(ITelegramBotClient bot,
        Message message,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        var user = message.From;
        if (user is null || string.IsNullOrWhiteSpace(message.Text)) return;

        var text = message.Text.Trim();

        var command = CommandRegex.Match(text);
        if (command.Success)
            switch (command
                        .Groups["command"]
                        .Value.ToLowerInvariant())
            {
                // может и есть получше способ для задания обработки команд, но я хз
                case "start":
                    var args = command.Groups["args"].Success
                        ? command
                            .Groups["args"]
                            .Value.Trim()
                        : string.Empty;
                    if (args.Length > 0)
                        await HandleStartWithArgsAsync(bot, message, user, args, cancellationToken);
                    else
                        await HandleStartAsync(bot, message, user, webAppUrl, cancellationToken);

                    return;

                case "roadie":
                    await HandleRoadiePingAsync(bot, message, user, cancellationToken);
                    return;

                case "ticket-roadie":
                    await HandleTicketRoadieAsync(bot, message, user, cancellationToken);
                    return;

                case "call-my-roadie":
                    await HandleCallMyRoadieAsync(bot, message, user, cancellationToken);
                    return;

                case "ping":
                    await HandlePingAsync(bot, message, user, cancellationToken);
                    return;

                case "help":
                    await SendTextAsync(bot,
                        message.Chat,
                        BotTexts.Get("help.start", user.LanguageCode),
                        cancellationToken);
                    return;
            }
    }

    private async Task HandlePingAsync(ITelegramBotClient bot,
        Message message,
        User user,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var isTopicMessage = message.IsTopicMessage;
        var isDirectMessage = message.Chat.IsDirectMessages;
        var topicId = message.MessageThreadId;
        if (!isTopicMessage ||
            isDirectMessage ||
            topicId == null ||
            chatId != long.Parse(telegramOptions.Value.ChatId))
            return;

        var topic = await songTopicRepository.FindByTopicIdAsync((long) topicId, cancellationToken);
        if (topic == null) return;

        var song = await songRepository.FindByIdWithDetailsAsync(topic.SongId, cancellationToken);
        if (song == null) return;

        if (message.Text == null) return;

        var (_, userMessage) = GetCommandArgsStr(message.Text);
        var text = string.IsNullOrEmpty(userMessage)
            ? $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> вызывает роуди!"
            : WebUtility.HtmlEncode(userMessage);

        text = song
            .Roles.Where(x => x.Assignment?.User.TgUserId != null)
            .Select(x => x.Assignment!.User)
            .DistinctBy(x => x.TgUserId)
            .Aggregate(text,
                (current, userToMention) => current + $"<a href=\"tg://user?id={userToMention.TgUserId}\">\u2060</a>");

        await bot.SendMessage(message.Chat.Id,
            $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> {text}",
            messageThreadId: (int) topicId,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Распаршивает команду на саму команду и аргументы после нее
    /// </summary>
    /// <param name="messageText"></param>
    /// <returns>Первое значение сама команда, второе значение текстовые аргументы после нее</returns>
    private (string?, string?) GetCommandArgsStr(string messageText)
    {
        var parts = messageText.Split([' '], 2);
        return (parts.Length >= 1 ? parts[0] : null, parts.Length >= 2 ? parts[1] : null);
    }

    private async Task HandleTicketRoadieAsync(ITelegramBotClient bot,
        Message message,
        User user,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var isTopicMessage = message.IsTopicMessage;
        var isDirectMessage = message.Chat.IsDirectMessages;
        var topicId = message.MessageThreadId;
        if (!isTopicMessage ||
            isDirectMessage ||
            topicId == null ||
            chatId != long.Parse(telegramOptions.Value.ChatId))
            return;

        var topic = await songTopicRepository.FindByTopicIdAsync((long) topicId, cancellationToken);
        if (topic == null) return;

        var song = await songRepository.FindByIdWithDetailsAsync(topic.SongId, cancellationToken);
        if (song == null) return;

        if (message.Text == null) return;

        var applicationUser = await userRepository.FindByTgUserIdAsync(user.Id, cancellationToken);
        if (applicationUser == null)
        {
            await bot.SendMessage(message.Chat.Id,
                $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a>, я не нашел тебя в моей базе данных, зайди в миниприложение разок и попробуй еще раз.",
                messageThreadId: message.MessageThreadId,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        try
        {
            await roadieService.CreateTicketAsync(song.Id, applicationUser, RoadieTicketType.Help, cancellationToken);
        }
        catch (ForbiddenAccessException e)
        {
            await bot.SendMessage(message.Chat.Id,
                $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a>, у тебя не хватило прав создать тикет(",
                messageThreadId: message.MessageThreadId,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }
        await bot.SendMessage(message.Chat.Id,
            $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a>, в чат роуди улетел запрос на помощь группе, ожидай.",
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleCallMyRoadieAsync(ITelegramBotClient bot,
        Message message,
        User user,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var isTopicMessage = message.IsTopicMessage;
        var isDirectMessage = message.Chat.IsDirectMessages;
        var topicId = message.MessageThreadId;
        if (!isTopicMessage ||
            isDirectMessage ||
            topicId == null ||
            chatId != long.Parse(telegramOptions.Value.ChatId))
            return;

        var topic = await songTopicRepository.FindByTopicIdAsync((long) topicId, cancellationToken);
        if (topic == null) return;

        var song = await songRepository.FindByIdWithDetailsAsync(topic.SongId, cancellationToken);
        if (song == null) return;

        if (message.Text == null) return;

        var roadie = await roadieService.GetRoadie(song, cancellationToken);
        if (roadie == null)
        {
            await bot.SendMessage(message.Chat.Id,
                $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a>, я не нашел у твоей песни роуди",
                messageThreadId: message.MessageThreadId,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);
            return;
        }

        var (_, userMessage) = GetCommandArgsStr(message.Text);
        var text = string.IsNullOrEmpty(userMessage)
            ? $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> вызывает своего роуди {telegramChatService.BuildUserMention(roadie!)}!"
            : WebUtility.HtmlEncode(userMessage);

        await bot.SendMessage(message.Chat.Id,
            $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> {text}",
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleRoadiePingAsync(ITelegramBotClient bot,
        Message message,
        User user,
        CancellationToken cancellationToken)
    {
        if (message.Text == null) return;

        var (_, userMessage) = GetCommandArgsStr(message.Text);
        var text = string.IsNullOrEmpty(userMessage)
            ? $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> вызывает роуди!"
            : WebUtility.HtmlEncode(userMessage);

        var roadies = await roadieService.ListRoadies(cancellationToken);
        var pingCount = 0;

        text = roadies.Aggregate(text, (current, userToMention) =>
        {
            pingCount++;
            return current + $"<a href=\"tg://user?id={userToMention.TgUserId}\">\u2060</a>";
        });

        text += $"\n\nБыло вызвано {pingCount} роуди";

        await bot.SendMessage(message.Chat.Id,
            $"<a href=\"tg://user?id={user.Id}\">{user.Username}</a> {text}",
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleStartAsync(ITelegramBotClient bot,
        Message message,
        User user,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Received command /start without args");

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithWebApp(BotTexts.Get("start.button", user.LanguageCode),
                    new WebAppInfo(webAppUrl)),
            },
        });

        await bot.SendMessage(message.Chat,
            BotTexts.Get("start.welcome", user.LanguageCode),
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleStartWithArgsAsync(ITelegramBotClient bot,
        Message message,
        User user,
        string args,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Received command start with {Args}", args);

        if (!args.StartsWith("auth_", StringComparison.Ordinal))
        {
            await SendTextAsync(bot,
                message.Chat,
                BotTexts.Get("start.invalid_param", user.LanguageCode),
                cancellationToken);
            return;
        }

        var rawUuid = args["auth_".Length..];
        if (!Guid.TryParse(rawUuid, out var token))
        {
            await SendTextAsync(bot,
                message.Chat,
                BotTexts.Get("start.invalid_token", user.LanguageCode),
                cancellationToken);
            return;
        }

        var link = await tgAuthLinks.FindByIdAsync(token, cancellationToken);
        if (link is not
            {
                TgUserId: null,
            })
        {
            await SendTextAsync(bot,
                message.Chat,
                BotTexts.Get("start.invalid_token", user.LanguageCode),
                cancellationToken);
            return;
        }

        link.TgUserId = user.Id;
        await tgAuthLinks.SaveChangesAsync(cancellationToken);

        await tgAuthService.UpsertUserAsync(user, cancellationToken);

        await SendTextAsync(bot, message.Chat, BotTexts.Get("auth.ok", user.LanguageCode), cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot,
        CallbackQuery callback,
        CancellationToken cancellationToken)
    {
        if (callback.Message is null)
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        const string roadieAcceptPrefix = "roadie_accept:";

        if (callback.Data?.StartsWith(roadieAcceptPrefix, StringComparison.Ordinal) == true &&
            Guid.TryParse(callback.Data[roadieAcceptPrefix.Length..], out var ticketId))
        {
            var tgUserId = callback.From?.Id ?? 0;

            if (tgUserId == 0)
            {
                await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
                return;
            }

            var result = await roadieService.AcceptTicketAsync(tgUserId, ticketId, cancellationToken);

            var text = result switch
            {
                RoadieAcceptResult.Accepted => "🎸 Вы взяли группу!",
                RoadieAcceptResult.NotARoadie => "Вы не роуди.",
                RoadieAcceptResult.AlreadyAccepted => "Заявка уже принята.",
                _ => "Заявка не найдена.",
            };

            await bot.AnswerCallbackQuery(callback.Id, text: text, cancellationToken: cancellationToken);

            if (result == RoadieAcceptResult.Accepted)
            {
                var acceptedBy = callback.From?.FirstName ?? "роуди";

                var newMarkup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData($"✅ Взял {acceptedBy}",
                    $"roadie_already_accepted:{ticketId}"));

                await bot.EditMessageReplyMarkup(callback.Message.Chat.Id,
                    callback.Message.MessageId,
                    replyMarkup: newMarkup,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }

    private static string NormalizeNamePart(string value)
    {
        return NamePartRegex
            .Replace(value, string.Empty)
            .ToLowerInvariant();
    }

    private static Task SendTextAsync(ITelegramBotClient bot,
        ChatId chatId,
        string text,
        CancellationToken cancellationToken)
    {
        return bot.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }
}
