using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.Song;

public partial class SongService
{
    private async Task ReplaceRolesAsync(Guid songId,
        IReadOnlyCollection<string> desiredRoles,
        CancellationToken cancellationToken)
    {
        var song = await db
            .Songs.Include(s => s.SongTopic)
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .FirstAsync(s => s.Id == songId, cancellationToken);

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
            await new SongServiceTopics(telegramChatService).AnnounceRoleRemovedAsync(song.SongTopic!.TopicId,
                songRole.RoleTitle,
                songRole.Assignment?.User,
                cancellationToken);
        }

        foreach (var role in toAdd)
        {
            await new SongServiceTopics(telegramChatService).AnnounceRoleAddedAsync(song.SongTopic!.TopicId,
                role,
                cancellationToken);

            db.SongRoles.Add(new SongRole
            {
                SongId = songId,
                RoleTitle = role,
            });
        }

        db.SongRoles.RemoveRange(toRemove);
    }
}
