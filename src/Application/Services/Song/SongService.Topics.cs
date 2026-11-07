using System.Net;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Telegram;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Application.Services.Song;

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

        var generalMessage = SongServiceFormatter.BuildSongFullMessage(song.Title, song.Artist, song.LinkUrl);
        var topicMessage = SongServiceFormatter.BuildSongFullTopicMessage(song.Title, song.Artist, song.LinkUrl, telegramChatService.BuildSongSystemLink(song), participants);

        if (!string.IsNullOrEmpty(generalMessage)) await telegramChatService.SendGeneralMessage(generalMessage, cancellationToken);
        if (!string.IsNullOrEmpty(topicMessage)) await telegramChatService.SendTopicMessage(topic.TopicId, topicMessage, cancellationToken);

        return topic;
    }

    public async Task AnnounceThumbnailUpdated(long topicId,
        string? url,
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        var mention = SongServiceFormatter.BuildParticipantMention(participantUser);
        if (url != null)
            await telegramChatService.SendTopicPhoto(topicId, url, $"{mention} обновил превьюшку песни", cancellationToken);
        else
            await telegramChatService.SendTopicMessage(topicId, $"{mention} удалил превьюшку песни", cancellationToken);
    }

    public async Task AnnounceRoleAddedAsync(long topicId,
        string roleTitle,
        CancellationToken cancellationToken = default)
    {
        await telegramChatService.SendTopicMessage(topicId,
            $"Была добавлена новая роль {roleTitle}",
            cancellationToken);
    }

    public async Task AnnounceRoleRemovedAsync(long topicId,
        string roleTitle,
        ApplicationUser? user = null,
        CancellationToken cancellationToken = default)
    {
        if (user == null)
            await telegramChatService.SendTopicMessage(topicId,
                $"Была удалена роль {roleTitle}",
                cancellationToken);
        else
        {
            var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
            var mention = SongServiceFormatter.BuildParticipantMention(participantUser);
            await telegramChatService.SendTopicMessage(topicId, $"Была удалена роль {roleTitle} вместе с {mention}, занимающим ее.", cancellationToken);
        }
    }

    /// <summary>
    /// Sends a notification to an existing topic when a participant joins.
    /// </summary>
    /// <param name="actor">Кто изменил состав; если это не сам участник, сообщение говорит, кто кого добавил.</param>
    public async Task AnnounceParticipantJoinAsync(
        long topicId,
        ApplicationUser user,
        string roleTitle,
        ApplicationUser? actor = null,
        CancellationToken cancellationToken = default)
    {
        var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        var mention = SongServiceFormatter.BuildParticipantMention(participantUser);
        var role = WebUtility.HtmlEncode(roleTitle.Trim());

        var text = actor is not null && actor.Id != user.Id
            ? $"{ActorName(actor)} добавил(а) {mention} в песню как {role}"
            : $"{mention} присоединился к песне как {role}";
        await telegramChatService.SendTopicMessage(topicId, text, cancellationToken);
    }

    /// <summary>
    /// Sends a notification to an existing topic when a participant leaves.
    /// </summary>
    /// <param name="actor">Кто изменил состав; если это не сам участник, сообщение говорит, кто кого убрал.</param>
    public async Task AnnounceParticipantLeaveAsync(
        long topicId,
        ApplicationUser user,
        string roleTitle,
        ApplicationUser? actor = null,
        CancellationToken cancellationToken = default)
    {
        var participantUser = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        var mention = SongServiceFormatter.BuildParticipantMention(participantUser);
        var role = WebUtility.HtmlEncode(roleTitle.Trim());

        var text = actor is not null && actor.Id != user.Id
            ? $"{ActorName(actor)} убрал(а) {mention} с роли {role}"
            : $"{mention} покинул песню как {role}";
        await telegramChatService.SendTopicMessage(topicId, text, cancellationToken);
    }

    // Имя без tg-ссылки: автор изменения и так знает, что сделал, пинговать его незачем
    private static string ActorName(ApplicationUser actor) => WebUtility.HtmlEncode(actor.DisplayName.Trim());
}
