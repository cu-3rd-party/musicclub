using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
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
    ILogger<BotUpdateHandler> logger,
    BotRehearsalCommandsHandler rehearsalCommandsHandler,
    BotScheduleCommandsHandler scheduleCommandsHandler)
{
    /// <summary>
    /// Сколько живёт ссылка входа t.me/bot?start=auth_… (браузер ждёт подтверждения).
    /// </summary>
    public static readonly TimeSpan AuthLinkLifetime = TelegramAuthService.AuthLinkLifetime;

    private const string AuthCallbackPrefix = "auth:";
    private const string RoadieAcceptPrefix = "roadie_accept:";

    private static readonly Regex CommandRegex = new(
        @"^\/(?<command>[a-z0-9_]+)(?:@(?<botusername>[a-zA-Z0-9_]+))?(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Команды, которые пингуют людей, — не чаще раза в интервал на пользователя.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, TimeSpan> Cooldowns = new Dictionary<string, TimeSpan>
    {
        ["roadie"] = TimeSpan.FromMinutes(2),
        ["ping"] = TimeSpan.FromMinutes(1),
        ["call_my_roadie"] = TimeSpan.FromMinutes(1),
        ["ticket_roadie"] = TimeSpan.FromMinutes(5),
    };

    private static readonly ConcurrentDictionary<(long UserId, string Command), DateTimeOffset> LastUsed = new();

    public async Task HandleUpdateAsync(ITelegramBotClient bot,
        Update update,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        if (update.Message is { } message)
        {
            await HandleMessageAsync(bot, message, webAppUrl, cancellationToken);
            return;
        }

        if (update.CallbackQuery is { } callback)
            await HandleCallbackQueryAsync(bot, callback, cancellationToken);
    }

    private async Task HandleMessageAsync(ITelegramBotClient bot,
        Message message,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        var user = message.From;
        if (user is null || user.IsBot || string.IsNullOrWhiteSpace(message.Text)) return;

        var match = CommandRegex.Match(message.Text.Trim());
        if (!match.Success) return;

        // «/slots@other_bot» адресована другому боту в том же чате
        var addressee = match.Groups["botusername"];
        var ourUsername = telegramOptions.Value.BotUsername.TrimStart('@');
        if (addressee.Success && ourUsername.Length > 0 &&
            !addressee.Value.Equals(ourUsername, StringComparison.OrdinalIgnoreCase))
            return;

        var command = match.Groups["command"].Value.ToLowerInvariant();
        var args = match.Groups["args"].Success ? match.Groups["args"].Value.Trim() : string.Empty;
        logger.LogDebug("Команда /{Command} от {UserId} в чате {ChatId}", command, user.Id, message.Chat.Id);

        if (Cooldowns.TryGetValue(command, out var cooldown) && !TryUse(user.Id, command, cooldown, out var wait))
        {
            await ReplyAsync(bot, message, string.Format(BotTexts.Cooldown, (int) Math.Ceiling(wait.TotalSeconds)),
                cancellationToken);
            return;
        }

        switch (command)
        {
            case "start":
                if (args.Length > 0)
                    await HandleStartWithArgsAsync(bot, message, args, cancellationToken);
                else
                    await HandleStartAsync(bot, message, webAppUrl, cancellationToken);
                return;

            case "help":
            case "info":
                await ReplyAsync(bot, message, BotTexts.Help, cancellationToken);
                return;

            case "roadie":
                await HandleRoadiePingAsync(bot, message, user, args, cancellationToken);
                return;

            case "ticket_roadie":
                await HandleTicketRoadieAsync(bot, message, user, cancellationToken);
                return;

            case "call_my_roadie":
                await HandleCallMyRoadieAsync(bot, message, user, args, cancellationToken);
                return;

            case "ping":
                await HandlePingAsync(bot, message, user, args, cancellationToken);
                return;

            // === Бронирование репетиций ===
            case "check":
            case "slots":
            case "slots_with":
            case "take":
            case "take_with":
            case "approve":
            case "reject":
            case "cancel":
                await rehearsalCommandsHandler.HandleRehearsalCommandAsync(
                    command, args, message, user, cancellationToken);
                return;

            // === Расписание ===
            case "status":
            case "update":
            case "day":
                await scheduleCommandsHandler.HandleScheduleCommandAsync(
                    command, args, message, user, cancellationToken);
                return;

            default:
                // В группах молчим: команда может быть для другого бота
                if (message.Chat.Type == ChatType.Private)
                    await ReplyAsync(bot, message, BotTexts.UnknownCommand, cancellationToken);
                return;
        }
    }

    private async Task HandlePingAsync(ITelegramBotClient bot,
        Message message,
        User user,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var song = await ResolveClubSongAsync(bot, message, cancellationToken);
        if (song == null) return;

        var participants = song
            .Roles.Where(x => x.Assignment?.User.TgUserId != null)
            .Select(x => x.Assignment!.User)
            .DistinctBy(x => x.TgUserId)
            .Where(x => x.TgUserId != user.Id)
            .ToList();

        var text = $"<b>🎤 {Html(song.Title)}</b>\n{Mention(user)} зовёт участников";

        if (!string.IsNullOrEmpty(userMessage))
            text += $"\n\n💬 <i>{Html(userMessage)}</i>";

        text += $"\n\n👥 Позвали: {participants.Count}";
        text += string.Concat(participants.Select(p => HiddenMention(p.TgUserId!.Value)));

        await ReplyAsync(bot, message, text, cancellationToken);
    }

    private async Task HandleTicketRoadieAsync(ITelegramBotClient bot,
        Message message,
        User user,
        CancellationToken cancellationToken)
    {
        var song = await ResolveClubSongAsync(bot, message, cancellationToken);
        if (song == null) return;

        var applicationUser = await userRepository.FindByTgUserIdAsync(user.Id, cancellationToken);
        if (applicationUser == null)
        {
            await ReplyAsync(bot, message, $"{Mention(user)}, {BotTexts.NotRegistered}", cancellationToken);
            return;
        }

        try
        {
            await roadieService.CreateTicketAsync(song.Id, applicationUser, RoadieTicketType.Help, cancellationToken);
        }
        catch (ForbiddenAccessException)
        {
            await ReplyAsync(bot, message, $"{Mention(user)}, у вас нет прав создать заявку для этой песни.",
                cancellationToken);
            return;
        }

        await ReplyAsync(bot, message,
            $"{Mention(user)}, заявка на помощь улетела в чат роуди — ждите отклика 🙌",
            cancellationToken);
    }

    private async Task HandleCallMyRoadieAsync(ITelegramBotClient bot,
        Message message,
        User user,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var song = await ResolveClubSongAsync(bot, message, cancellationToken);
        if (song == null) return;

        var roadie = await roadieService.GetRoadie(song, cancellationToken);
        if (roadie == null)
        {
            await ReplyAsync(bot, message,
                $"{Mention(user)}, у этой песни пока нет роуди. Попросить — /ticket_roadie",
                cancellationToken);
            return;
        }

        var text = $"{Mention(user)} зовёт роуди {telegramChatService.BuildUserMention(roadie)}";
        if (!string.IsNullOrEmpty(userMessage))
            text += $"\n\n💬 <i>{Html(userMessage)}</i>";

        await ReplyAsync(bot, message, text, cancellationToken);
    }

    private async Task HandleRoadiePingAsync(ITelegramBotClient bot,
        Message message,
        User user,
        string userMessage,
        CancellationToken cancellationToken)
    {
        // Пинг всех роуди — только из чата клуба, чтобы бота нельзя было использовать для спама
        if (!IsClubChat(message.Chat.Id))
        {
            await ReplyAsync(bot, message, BotTexts.ClubChatOnly, cancellationToken);
            return;
        }

        var roadies = (await roadieService.ListRoadies(cancellationToken))
            .Where(r => r.TgUserId != null && r.TgUserId != user.Id)
            .ToList();

        var text = $"{Mention(user)} зовёт роуди!";
        if (!string.IsNullOrEmpty(userMessage))
            text += $"\n\n💬 <i>{Html(userMessage)}</i>";

        text += $"\n\n🙋 Позвали роуди: {roadies.Count}";
        text += string.Concat(roadies.Select(r => HiddenMention(r.TgUserId!.Value)));

        await ReplyAsync(bot, message, text, cancellationToken);
    }

    private async Task HandleStartAsync(ITelegramBotClient bot,
        Message message,
        string webAppUrl,
        CancellationToken cancellationToken)
    {
        // WebApp-кнопки Telegram разрешает только в личке; в группе — обычная ссылка
        var button = message.Chat.Type == ChatType.Private
            ? InlineKeyboardButton.WithWebApp(BotTexts.StartButton, new WebAppInfo(webAppUrl))
            : InlineKeyboardButton.WithUrl(BotTexts.StartButton, webAppUrl);

        await bot.SendMessage(message.Chat,
            BotTexts.Welcome,
            messageThreadId: message.MessageThreadId,
            replyMarkup: new InlineKeyboardMarkup(button),
            cancellationToken: cancellationToken);
    }

    private async Task HandleStartWithArgsAsync(ITelegramBotClient bot,
        Message message,
        string args,
        CancellationToken cancellationToken)
    {
        if (!args.StartsWith("auth_", StringComparison.Ordinal))
        {
            await ReplyAsync(bot, message, BotTexts.StartInvalidParam, cancellationToken);
            return;
        }

        if (message.Chat.Type != ChatType.Private)
        {
            await ReplyAsync(bot, message, BotTexts.AuthPrivateOnly, cancellationToken);
            return;
        }

        if (!Guid.TryParse(args["auth_".Length..], out var token) ||
            await FindUsableAuthLinkAsync(token, cancellationToken) == null)
        {
            await ReplyAsync(bot, message, BotTexts.AuthInvalidToken, cancellationToken);
            return;
        }

        // Не привязываем сразу: ссылку мог прислать злоумышленник, чтобы войти под чужим аккаунтом
        var keyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData(BotTexts.AuthConfirmButton, $"{AuthCallbackPrefix}ok:{token:N}"),
                InlineKeyboardButton.WithCallbackData(BotTexts.AuthCancelButton, $"{AuthCallbackPrefix}no:{token:N}"),
            ],
        ]);

        await bot.SendMessage(message.Chat,
            BotTexts.AuthConfirm,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task HandleAuthCallbackAsync(ITelegramBotClient bot,
        CallbackQuery callback,
        CancellationToken cancellationToken)
    {
        var parts = callback.Data![AuthCallbackPrefix.Length..].Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var token) ||
            callback.Message?.Chat.Type != ChatType.Private)
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        var link = await FindUsableAuthLinkAsync(token, cancellationToken);
        string result;

        if (link == null)
            result = BotTexts.AuthInvalidToken;
        else if (parts[0] == "ok")
        {
            link.TgUserId = callback.From.Id;
            await tgAuthLinks.SaveChangesAsync(cancellationToken);
            await tgAuthService.UpsertUserAsync(callback.From, cancellationToken);
            result = BotTexts.AuthOk;
        }
        else
        {
            tgAuthLinks.Remove(link);
            await tgAuthLinks.SaveChangesAsync(cancellationToken);
            result = BotTexts.AuthCancelled;
        }

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
        await bot.EditMessageText(callback.Message.Chat.Id,
            callback.Message.MessageId,
            result,
            cancellationToken: cancellationToken);
    }

    private async Task<TgAuthLink?> FindUsableAuthLinkAsync(Guid token, CancellationToken cancellationToken)
    {
        var link = await tgAuthLinks.FindByIdAsync(token, cancellationToken);
        if (link is not { TgUserId: null }) return null;

        return DateTimeOffset.UtcNow - link.Created > AuthLinkLifetime ? null : link;
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot,
        CallbackQuery callback,
        CancellationToken cancellationToken)
    {
        var data = callback.Data ?? string.Empty;

        if (data.StartsWith(AuthCallbackPrefix, StringComparison.Ordinal))
        {
            await HandleAuthCallbackAsync(bot, callback, cancellationToken);
            return;
        }

        if (data.StartsWith(BotRehearsalCommandsHandler.CallbackPrefix, StringComparison.Ordinal))
        {
            await rehearsalCommandsHandler.HandleCallbackAsync(callback, cancellationToken);
            return;
        }

        if (callback.Message is not null &&
            data.StartsWith(RoadieAcceptPrefix, StringComparison.Ordinal) &&
            Guid.TryParse(data[RoadieAcceptPrefix.Length..], out var ticketId))
        {
            var result = await roadieService.AcceptTicketAsync(callback.From.Id, ticketId, cancellationToken);

            var text = result switch
            {
                RoadieAcceptResult.Accepted => "🎸 Вы взяли группу!",
                RoadieAcceptResult.NotARoadie => "Брать группы могут только роуди.",
                RoadieAcceptResult.AlreadyAccepted => "Эту заявку уже кто-то взял.",
                _ => "Заявка не найдена.",
            };

            await bot.AnswerCallbackQuery(callback.Id, text: text, cancellationToken: cancellationToken);

            if (result == RoadieAcceptResult.Accepted)
            {
                var newMarkup = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(
                    $"✅ Взял(а) {callback.From.FirstName}",
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

    /// <summary>
    /// Песня топика в чате клуба. Отвечает пользователю, если команда отправлена не туда.
    /// </summary>
    private async Task<Song?> ResolveClubSongAsync(ITelegramBotClient bot,
        Message message,
        CancellationToken cancellationToken)
    {
        if (!IsClubChat(message.Chat.Id))
        {
            await ReplyAsync(bot, message, BotTexts.ClubChatOnly, cancellationToken);
            return null;
        }

        if (!message.IsTopicMessage || message.MessageThreadId is not { } topicId)
        {
            await ReplyAsync(bot, message, BotTexts.SongTopicOnly, cancellationToken);
            return null;
        }

        var topic = await songTopicRepository.FindByTopicIdAsync(topicId, cancellationToken);
        var song = topic == null
            ? null
            : await songRepository.FindByIdWithDetailsAsync(topic.SongId, cancellationToken);

        if (song == null)
            await ReplyAsync(bot, message, BotTexts.SongTopicOnly, cancellationToken);

        return song;
    }

    private bool IsClubChat(long chatId)
    {
        return long.TryParse(telegramOptions.Value.ChatId, out var clubChatId) && chatId == clubChatId;
    }

    private static bool TryUse(long userId, string command, TimeSpan cooldown, out TimeSpan wait)
    {
        var now = DateTimeOffset.UtcNow;
        var key = (userId, command);
        wait = TimeSpan.Zero;

        if (LastUsed.TryGetValue(key, out var last) && now - last < cooldown)
        {
            wait = cooldown - (now - last);
            return false;
        }

        LastUsed[key] = now;
        return true;
    }

    private static string Mention(User user)
    {
        var name = string.IsNullOrWhiteSpace(user.FirstName) ? user.Username ?? "участник" : user.FirstName;
        return $"<a href=\"tg://user?id={user.Id}\">{Html(name)}</a>";
    }

    /// <summary>
    /// Невидимое упоминание: человеку приходит уведомление, а текст не засоряется.
    /// </summary>
    private static string HiddenMention(long tgUserId)
    {
        return $"<a href=\"tg://user?id={tgUserId}\">⁠</a>";
    }

    private static string Html(string? text)
    {
        return WebUtility.HtmlEncode(text ?? "");
    }

    private Task<Message> ReplyAsync(ITelegramBotClient bot,
        Message message,
        string html,
        CancellationToken cancellationToken)
    {
        return bot.SendMessage(message.Chat,
            html,
            messageThreadId: message.MessageThreadId,
            parseMode: ParseMode.Html,
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: cancellationToken);
    }
}
