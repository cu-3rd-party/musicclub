using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Song;

public partial class SongService
{
    private async Task ReplaceRolesAsync(Guid songId,
        IReadOnlyCollection<string> desiredRoles,
        CancellationToken cancellationToken)
    {
        var song = await songs.FindByIdWithTopicAndRolesAsync(songId, cancellationToken)
                   ?? throw new NotFoundException(songId.ToString(), nameof(Domain.Entities.Song));

        var currentRoleTitles = song.Roles
            .Select(r => r.RoleTitle)
            .ToHashSet(StringComparer.Ordinal);

        var desiredSet = desiredRoles.ToHashSet(StringComparer.Ordinal);

        var toRemove = song.Roles
            .Where(role => !desiredSet.Contains(role.RoleTitle))
            .ToList();

        var toAdd = desiredSet
            .Where(role => !currentRoleTitles.Contains(role))
            .ToList();

        foreach (var songRole in toRemove)
        {
            if (song.SongTopic != null)
                await new SongServiceTopics(telegramChatService).AnnounceRoleRemovedAsync(song.SongTopic.TopicId,
                    songRole.RoleTitle,
                    songRole.Assignment?.User,
                    cancellationToken);
        }

        foreach (var role in toAdd)
        {
            if (song.SongTopic != null)
                await new SongServiceTopics(telegramChatService).AnnounceRoleAddedAsync(song.SongTopic.TopicId,
                    role,
                    cancellationToken);

            await songRoles.AddAsync(new SongRole
            {
                SongId = songId,
                RoleTitle = role,
            }, cancellationToken);
        }

        foreach (var songRole in toRemove) songRoles.Remove(songRole);

        // After removing roles, check if the song is now full (all remaining roles are filled)
        // and create a topic if one doesn't exist yet.
        // This handles the corner case where deleting the only unfilled role makes the song full.
        if (song.SongTopic == null && song.IsFull)
        {
            // Reload the song with full details to ensure we have the latest state
            var fullSong = await songs.FindByIdWithDetailsAsync(songId, cancellationToken)
                           ?? throw new NotFoundException(songId.ToString(), nameof(Domain.Entities.Song));

            await new SongServiceTopics(telegramChatService).CreateTopicForFullSongAsync(fullSong, cancellationToken);
        }
    }
}