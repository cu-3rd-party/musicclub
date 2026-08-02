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
    private const string CallbackAttachCalendar = "calendar_attach";
    private const string CallbackEmailConfirmYes = "email_confirm_yes";
    private const string CallbackEmailConfirmNo = "email_confirm_no";

    private const short StateCalendarUrl = 1;
    private const short StateEmailGuess = 2;
    private const short StateEmailInput = 3;

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

        await HandleCalendarAttachMessageAsync(bot, message, user, text, cancellationToken);
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

        var ok = await ConfirmAuthAsync(token, user.Id, cancellationToken);
        await SendTextAsync(bot, message.Chat, BotTexts.Get(ok ? "auth.ok" : "auth.fail", user.LanguageCode), cancellationToken);
    }

    private async Task HandleCallbackQueryAsync(
        ITelegramBotClient bot, CallbackQuery callback, CancellationToken cancellationToken)
    {
        var user = callback.From;
        if (user is null || callback.Message is null)
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        switch (callback.Data)
        {
            case CallbackAttachCalendar:
                await HandleAttachCalendarCallbackAsync(bot, callback, user, cancellationToken);
                break;
            case CallbackEmailConfirmYes:
                await HandleEmailConfirmYesAsync(bot, callback, user, cancellationToken);
                break;
            case CallbackEmailConfirmNo:
                await HandleEmailConfirmNoAsync(bot, callback, user, cancellationToken);
                break;
        }
    }

    private async Task HandleAttachCalendarCallbackAsync(
        ITelegramBotClient bot, CallbackQuery callback, User user, CancellationToken cancellationToken)
    {
        var profile = await GetUserByTgIdAsync(user.Id, cancellationToken);
        if (profile is null)
        {
            await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("calendar.attach.not_linked", user.LanguageCode), cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        if (!string.IsNullOrEmpty(profile.Email))
        {
            await UpsertStateAsync(user.Id, StateCalendarUrl, null, null, cancellationToken);
            await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("calendar.attach.ask", user.LanguageCode), cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        var guess = GuessEmailFromName(profile.DisplayName, user.FirstName, user.LastName);
        if (guess is not null)
        {
            await UpsertStateAsync(user.Id, StateEmailGuess, profile.Id, guess, cancellationToken);
            var prompt = string.Format(BotTexts.Get("email.confirm.prompt", user.LanguageCode), guess);
            await bot.SendMessage(
                callback.Message!.Chat,
                prompt,
                replyMarkup: EmailConfirmKeyboard(user.LanguageCode),
                cancellationToken: cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        await UpsertStateAsync(user.Id, StateEmailInput, null, null, cancellationToken);
        await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("email.ask", user.LanguageCode), cancellationToken);
        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleCalendarAttachMessageAsync(
        ITelegramBotClient bot, Message message, User user, string text, CancellationToken cancellationToken)
    {
        var pending = await GetStateAsync(user.Id, cancellationToken);

        if (pending is { State: StateEmailInput })
        {
            if (!IsValidEmail(text))
            {
                await SendTextAsync(bot, message.Chat, BotTexts.Get("email.invalid", user.LanguageCode), cancellationToken);
                return;
            }

            var profile = await GetUserByTgIdAsync(user.Id, cancellationToken);
            if (profile is null)
            {
                await ClearStateAsync(user.Id, cancellationToken);
                await SendTextAsync(bot, message.Chat, BotTexts.Get("calendar.attach.not_linked", user.LanguageCode), cancellationToken);
                return;
            }

            var saved = await UpdateUserEmailAsync(profile.Id, text, cancellationToken);
            if (!saved)
            {
                await SendTextAsync(bot, message.Chat, BotTexts.Get("email.save.fail", user.LanguageCode), cancellationToken);
                return;
            }

            await SendTextAsync(bot, message.Chat, string.Format(BotTexts.Get("email.save.ok", user.LanguageCode), text), cancellationToken);
            await UpsertStateAsync(user.Id, StateCalendarUrl, null, null, cancellationToken);
            await SendTextAsync(bot, message.Chat, BotTexts.Get("calendar.attach.ask", user.LanguageCode), cancellationToken);
            return;
        }

        if (pending is null || pending.State != StateCalendarUrl)
        {
            return;
        }

        if (!IsValidIcsUrl(text))
        {
            await SendTextAsync(bot, message.Chat, BotTexts.Get("calendar.attach.invalid_url", user.LanguageCode), cancellationToken);
            return;
        }

        var userId = await GetUserIdByTgIdAsync(user.Id, cancellationToken);
        if (userId is null)
        {
            await ClearStateAsync(user.Id, cancellationToken);
            await SendTextAsync(bot, message.Chat, BotTexts.Get("calendar.attach.not_linked", user.LanguageCode), cancellationToken);
            return;
        }

        var ok = await UpsertCalendarUrlAsync(userId.Value, text, cancellationToken);
        await ClearStateAsync(user.Id, cancellationToken);
        await SendTextAsync(bot, message.Chat, BotTexts.Get(ok ? "calendar.attach.success" : "calendar.attach.fail", user.LanguageCode), cancellationToken);
    }

    private async Task HandleEmailConfirmYesAsync(
        ITelegramBotClient bot, CallbackQuery callback, User user, CancellationToken cancellationToken)
    {
        var pending = await GetStateAsync(user.Id, cancellationToken);
        if (pending is not { State: StateEmailGuess })
        {
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        if (pending.PendingUserId is null || pending.PendingEmail is null)
        {
            await ClearStateAsync(user.Id, cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        var saved = await UpdateUserEmailAsync(pending.PendingUserId.Value, pending.PendingEmail, cancellationToken);
        if (!saved)
        {
            await ClearStateAsync(user.Id, cancellationToken);
            await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("email.save.fail", user.LanguageCode), cancellationToken);
            await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
            return;
        }

        await SendTextAsync(bot, callback.Message!.Chat, string.Format(BotTexts.Get("email.save.ok", user.LanguageCode), pending.PendingEmail), cancellationToken);
        await UpsertStateAsync(user.Id, StateCalendarUrl, null, null, cancellationToken);
        await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("calendar.attach.ask", user.LanguageCode), cancellationToken);
        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }

    private async Task HandleEmailConfirmNoAsync(
        ITelegramBotClient bot, CallbackQuery callback, User user, CancellationToken cancellationToken)
    {
        var pending = await GetStateAsync(user.Id, cancellationToken);
        if (pending is { State: StateEmailGuess })
        {
            await UpsertStateAsync(user.Id, StateEmailInput, null, null, cancellationToken);
            await SendTextAsync(bot, callback.Message!.Chat, BotTexts.Get("email.ask", user.LanguageCode), cancellationToken);
        }

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);
    }

    private async Task<bool> ConfirmAuthAsync(Guid token, long telegramUserId, CancellationToken cancellationToken)
    {
        try
        {
            var auth = await _db.TgAuthUsers.FirstOrDefaultAsync(u => u.Id == token, cancellationToken);
            if (auth is null)
            {
                _logger.LogInformation("No auth request found for token {Token}", token);
                return false;
            }

            if (auth.Success)
            {
                _logger.LogInformation("Auth token {Token} already used", token);
                return false;
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == auth.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("User {UserId} for auth token {Token} not found", auth.UserId, token);
                return false;
            }

            auth.TgUserId = telegramUserId;
            auth.Success = true;
            user.TgUserId = telegramUserId;
            await _userManager.UpdateAsync(user);

            var existing = await _userManager.GetClaimsAsync(user);
            var granted = existing
                .Where(c => c.Type == PermissionClaimTypes.Permission)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);
            var toAdd = Permissions.All
                .Where(p => !granted.Contains(p))
                .Select(p => new Claim(PermissionClaimTypes.Permission, p))
                .ToList();
            if (toAdd.Count > 0)
            {
                await _userManager.AddClaimsAsync(user, toAdd);
            }

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Auth confirmed for token {Token} and telegram user {TelegramUserId}", token, telegramUserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm auth for token {Token}", token);
            return false;
        }
    }

    private async Task<PendingState?> GetStateAsync(long tgUserId, CancellationToken cancellationToken)
    {
        var state = await _db.CalendarAttachStates
            .FirstOrDefaultAsync(s => s.TgUserId == tgUserId, cancellationToken);
        return state is null
            ? null
            : new PendingState(state.State, state.PendingUserId, state.PendingEmail);
    }

    private async Task UpsertStateAsync(
        long tgUserId, short state, Guid? pendingUserId, string? pendingEmail, CancellationToken cancellationToken)
    {
        var existing = await _db.CalendarAttachStates
            .FirstOrDefaultAsync(s => s.TgUserId == tgUserId, cancellationToken);

        if (existing is null)
        {
            _db.Add(new CalendarAttachState
            {
                TgUserId = tgUserId,
                State = state,
                PendingUserId = pendingUserId,
                PendingEmail = pendingEmail,
            });
        }
        else
        {
            existing.State = state;
            existing.PendingUserId = pendingUserId;
            existing.PendingEmail = pendingEmail;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearStateAsync(long tgUserId, CancellationToken cancellationToken)
    {
        var existing = await _db.CalendarAttachStates
            .FirstOrDefaultAsync(s => s.TgUserId == tgUserId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _db.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserProfile?> GetUserByTgIdAsync(long tgUserId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.TgUserId == tgUserId, cancellationToken);
        return user is null
            ? null
            : new UserProfile(user.Id, user.DisplayName, user.Email);
    }

    private async Task<Guid?> GetUserIdByTgIdAsync(long tgUserId, CancellationToken cancellationToken)
    {
        var id = await _userManager.Users
            .Where(u => u.TgUserId == tgUserId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return id;
    }

    private async Task<bool> UpdateUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                return false;
            }

            var result = await _userManager.SetEmailAsync(user, email);
            return result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update email for user {UserId}", userId);
            return false;
        }
    }

    private async Task<bool> UpsertCalendarUrlAsync(Guid userId, string calendarUrl, CancellationToken cancellationToken)
    {
        try
        {
            var calendar = await _db.Calendars.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            if (calendar is null)
            {
                _db.Add(new Calendar { UserId = userId, CalendarUrl = calendarUrl });
            }
            else
            {
                calendar.CalendarUrl = calendarUrl;
                calendar.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert calendar url for user {UserId}", userId);
            return false;
        }
    }

    private static bool IsValidIcsUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        return uri.AbsolutePath.Contains(".ics", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidEmail(string value)
    {
        return EmailRegex.IsMatch(value.Trim());
    }

    private static string NormalizeNamePart(string value)
    {
        return NamePartRegex.Replace(value, string.Empty).ToLowerInvariant();
    }

    private string? GuessEmailFromName(string? displayName, string? fallbackFirst, string? fallbackLast)
    {
        var tokens = string.IsNullOrWhiteSpace(displayName)
            ? []
            : displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string? first;
        string? last;
        if (tokens.Length >= 2)
        {
            first = tokens[0];
            last = tokens[^1];
        }
        else
        {
            first = tokens.Length > 0 ? tokens[0] : fallbackFirst;
            last = fallbackLast;
        }

        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
        {
            return null;
        }

        var firstNorm = NormalizeNamePart(first);
        var lastNorm = NormalizeNamePart(last);
        if (firstNorm.Length == 0 || lastNorm.Length == 0)
        {
            return null;
        }

        return $"{firstNorm[0]}.{lastNorm}@{_options.EmailDomain}";
    }

    private static InlineKeyboardMarkup EmailConfirmKeyboard(string? languageCode)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotTexts.Get("email.confirm.yes", languageCode), CallbackEmailConfirmYes),
                InlineKeyboardButton.WithCallbackData(BotTexts.Get("email.confirm.no", languageCode), CallbackEmailConfirmNo),
            },
        });
    }

    private static Task SendTextAsync(
        ITelegramBotClient bot, ChatId chatId, string text, CancellationToken cancellationToken)
    {
        return bot.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }

    private sealed record PendingState(short State, Guid? PendingUserId, string? PendingEmail);

    private sealed record UserProfile(Guid Id, string DisplayName, string? Email);
}
