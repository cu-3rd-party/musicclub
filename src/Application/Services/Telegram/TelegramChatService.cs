using System.Net;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CuMusicClub.Application.Services.Telegram;

public class TelegramChatService(
    ITelegramBotClient bot,
    IOptions<TelegramOptions> telegramOptions,
    IOptions<BotOptions> botOptions,
    ISongRepository songs,
    ISongTopicRepository songTopics) : ITelegramChatService
{
    private readonly long _chatId = long.Parse(telegramOptions.Value.ChatId);

    private readonly long _roadieChatId = string.IsNullOrEmpty(telegramOptions.Value.RoadieChatId)
        ? long.Parse(telegramOptions.Value.ChatId)
        : long.Parse(telegramOptions.Value.RoadieChatId);

    public async Task<SongTopic> CreateTopic(string title, Guid songId, CancellationToken cancellationToken = default)
    {
        var forumTopic = await bot.CreateForumTopic(_chatId, title, cancellationToken: cancellationToken);

        var song = await songs.FindByIdAsync(songId);
        if (song is null)
        {
            throw new InvalidOperationException($"Song with id {songId} not found");
        }

        var songTopic = new SongTopic
        {
            Song = song,
            SongId = songId,
            TopicId = forumTopic.MessageThreadId,
            ChatId = _chatId,
            Title = title,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await songTopics.AddAsync(songTopic, cancellationToken);
        await songTopics.SaveChangesAsync(cancellationToken);

        return songTopic;
    }

    public async Task<SongTopic> GetTopic(long topicId, CancellationToken cancellationToken = default)
    {
        return await songTopics.FindByTopicIdAsync(topicId, cancellationToken);
    }

    public async Task DeleteTopic(long topicId, CancellationToken cancellationToken = default)
    {
        await bot.CloseForumTopic(_chatId, (int) topicId, cancellationToken: cancellationToken);
        await bot.DeleteForumTopic(_chatId, (int) topicId, cancellationToken: cancellationToken);

        var topic = await GetTopic(topicId, cancellationToken);
        songTopics.Remove(topic);
        await songTopics.SaveChangesAsync(cancellationToken);
    }

    public async Task SendTopicMessage(long topicId, string message, CancellationToken cancellationToken = default)
    {
        await bot.SendMessage(_chatId,
            message,
            parseMode: ParseMode.Html,
            messageThreadId: (int) topicId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Добавляет https://dev.musicclub.cu3rd.ru/api/v1 перед url (или что стоит в TelegramOptions__BaseUrl)
    /// </summary>
    /// <param name="topicId"></param>
    /// <param name="url"></param>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="NotImplementedException"></exception>
    public async Task SendTopicPhoto(long topicId,
        string url,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var inputFile = new InputFileUrl(telegramOptions.Value.ImageBaseUrl + url);
        await bot.SendPhoto(_chatId,
            inputFile,
            message,
            parseMode: ParseMode.Html,
            messageThreadId: (int) topicId,
            cancellationToken: cancellationToken);
    }

    public async Task SendGeneralMessage(string message, CancellationToken cancellationToken = default)
    {
        await bot.SendMessage(_chatId, message, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
    }

    public async Task SendRoadieTicketNotification(Guid ticketId,
        string songTitle,
        string artist,
        IEnumerable<SongRole> songRoles,
        RoadieTicketType roadieTicketType = RoadieTicketType.Assignment,
        CancellationToken cancellationToken = default)
    {
        var members = string.Join("\n",
            songRoles
                .Where(x => x.Assignment?.User != null)
                .Select(x => $"{WebUtility.HtmlEncode(x.RoleTitle)} — {BuildUserMention(x.Assignment!.User)}"));

        var message = roadieTicketType switch
        {
            RoadieTicketType.Assignment =>
                $"🎸 Группе «{WebUtility.HtmlEncode(songTitle)} — {WebUtility.HtmlEncode(artist)}» " +
                $"нужен постоянный роуди!\n\n" +
                $"👥 Состав группы:\n" +
                $"{members}\n\n" +
                $"Нажмите «Взять группу», если придёте.",
            RoadieTicketType.Help =>
                $"🎸 Группе «{WebUtility.HtmlEncode(songTitle)} — {WebUtility.HtmlEncode(artist)}» " +
                $"нужна помощь роуди на разок.\n\n" +
                $"👥 Состав группы:\n" +
                $"{members}\n\n" +
                $"Нажмите «Взять группу», если придёте.",
            _ => throw new ArgumentOutOfRangeException()
        };

        var replyMarkup = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("Взять группу", $"roadie_accept:{ticketId}"));

        await bot.SendMessage(_roadieChatId,
            message,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public async Task SendRoadieMessage(string message, CancellationToken cancellationToken = default)
    {
        await bot.SendMessage(_roadieChatId, message, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
    }

    public async Task SendDirectMessage(long tgUserId, string message, CancellationToken cancellationToken = default)
    {
        await bot.SendMessage(new ChatId(tgUserId),
            message,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    public string BuildUserMention(ApplicationUser user)
    {
        string userTag;

        if (!string.IsNullOrWhiteSpace(user.UserName))
            userTag = $"@{WebUtility.HtmlEncode(user.UserName)}";
        else if (user.TgUserId.HasValue)
            userTag = $"<a href=\"tg://user?id={user.TgUserId.Value}\">" +
                      $"{WebUtility.HtmlEncode(user.DisplayName)}" +
                      $"</a>";
        else
            userTag = WebUtility.HtmlEncode(user.DisplayName);

        return userTag;
    }

    public string BuildSongSystemLink(Domain.Entities.Song song)
    {
        var url = botOptions.Value.WebAppUrl;
        if (url.StartsWith("https://"))
            url = url["https://".Length..];
        return $"{url}/app/songs/{song.Id}";
    }
}
