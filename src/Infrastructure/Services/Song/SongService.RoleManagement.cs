using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.Song;

public partial class SongService
{
    private async Task ReplaceRolesAsync(Guid songId,
        IReadOnlyCollection<string> desiredRoles,
        CancellationToken cancellationToken)
    {
        var currentRoles = await db
            .SongRoles.Where(r => r.SongId == songId)
            .Select(r => r.RoleTitle)
            .ToListAsync(cancellationToken);

        var desiredSet = desiredRoles.ToHashSet(StringComparer.Ordinal);

        var toRemove = currentRoles
            .Where(role => !desiredSet.Contains(role))
            .ToList();
        if (toRemove.Count > 0)
            await db
                .SongRoles.Where(r => r.SongId == songId && toRemove.Contains(r.RoleTitle))
                .ExecuteDeleteAsync(cancellationToken);

        foreach (var role in desiredSet.Where(role => !currentRoles.Contains(role)))
            db.SongRoles.Add(new SongRole
            {
                SongId = songId,
                RoleTitle = role,
            });
    }
}
