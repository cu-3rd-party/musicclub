using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CuMusicClub.Infrastructure.Services.Telegram;

public class TelegramChatService(
    IOptions<TelegramOptions> telegramOptions,
    ApplicationDbContext db) : ITelegramChatService
{
    private readonly ITelegramBotClient _bot = new TelegramBotClient(telegramOptions.Value.BotToken);
    private readonly long _chatId = long.Parse(telegramOptions.Value.ChatId);

    public async Task<SongTopic> CreateTopic(string title, Guid songId, CancellationToken cancellationToken = default)
    {
        var forumTopic = await _bot.CreateForumTopic(_chatId, title, cancellationToken: cancellationToken);

        var song = await db.Songs.FindAsync([songId], cancellationToken);
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

        await db.SongTopics.AddAsync(songTopic, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return songTopic;
    }

    public async Task<SongTopic> GetTopic(long topicId, CancellationToken cancellationToken = default)
    {
        return await db.SongTopics.FirstAsync(t => t.TopicId == topicId, cancellationToken);
    }

    public async Task DeleteTopic(long topicId, CancellationToken cancellationToken = default)
    {
        await _bot.CloseForumTopic(_chatId, (int)topicId, cancellationToken: cancellationToken);
        await _bot.DeleteForumTopic(_chatId, (int)topicId, cancellationToken: cancellationToken);

        var topic = await GetTopic(topicId, cancellationToken);
        db.SongTopics.Remove(topic);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendTopicMessage(long topicId, string message, CancellationToken cancellationToken = default)
    {
        await _bot.SendMessage(_chatId, message, parseMode: ParseMode.Html, messageThreadId: (int)topicId, cancellationToken: cancellationToken);
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
        await _bot.SendPhoto(_chatId,
            inputFile,
            message,
            parseMode: ParseMode.Html,
            messageThreadId: (int) topicId,
            cancellationToken: cancellationToken);
    }

    public async Task SendGeneralMessage(string message, CancellationToken cancellationToken = default)
    {
        await _bot.SendMessage(_chatId, message, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
    }
}
