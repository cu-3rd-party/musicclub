using System.Net;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.Song;

/// <summary>
/// Helper class for managing Telegram topic notifications for songs.
/// </summary>
public class SongServiceTopics(
    ITelegramChatService telegramChatService)
{
    /// <summary>
    /// Creates a new topic for a full song and announces all participants.
    /// </summary>
    public async Task<SongTopic> CreateTopicForFullSongAsync(
        Domain.Entities.Song song,
        CancellationToken cancellationToken = default)
    {
        var topicTitle = SongServiceFormatter.BuildSongTopicTitle(song.Title, song.Artist);
        var topic = await telegramChatService.CreateTopic(topicTitle, song.Id, cancellationToken);

        var participants = song.Roles
            .Where(r => r.Assignment != null)
            .Select(r => r.Assignment!)
            .Select(a => new RoleAssignmentDto(
                a.Id,
                new SongUserDto(a.User.Id, a.User.DisplayName, a.User.UserName, a.User.AvatarUrl, a.User.TgUserId),
                a.JoinedAt))
            .ToList();

        var message = SongServiceFormatter.BuildSongTopicMessage(song.Title, song.Artist, song.LinkUrl, participants);

        if (!string.IsNullOrEmpty(message))
        {
            await telegramChatService.SendTopicMessage(topic.TopicId, message, cancellationToken);
        }

        return topic;
    }

    /// <summary>
    /// Sends a notification to an existing topic when a participant joins.
    /// </summary>
    public async Task AnnounceParticipantJoinAsync(
        long topicId,
        ApplicationUser user,
        string roleTitle,
        CancellationToken cancellationToken = default)
    {
        var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        var mention = SongServiceFormatter.BuildParticipantMention(participantUser);

        await telegramChatService.SendTopicMessage(topicId, $"{mention} присоединился к песне как {WebUtility.HtmlEncode(roleTitle.Trim())}", cancellationToken);
    }

    /// <summary>
    /// Sends a notification to an existing topic when a participant leaves.
    /// </summary>
    public async Task AnnounceParticipantLeaveAsync(
        long topicId,
        ApplicationUser user,
        string roleTitle,
        CancellationToken cancellationToken = default)
    {
        var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        var mention = SongServiceFormatter.BuildParticipantMention(participantUser);

        await telegramChatService.SendTopicMessage(topicId, $"{mention} покинул песню как {WebUtility.HtmlEncode(roleTitle.Trim())}", cancellationToken);
    }
}
