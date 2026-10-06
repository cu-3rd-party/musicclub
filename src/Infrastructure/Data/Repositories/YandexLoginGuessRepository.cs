using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class YandexLoginGuessRepository(DbContext dbContext)
    : Repository<YandexLoginGuess>(dbContext), IYandexLoginGuessRepository
{
    public async Task<YandexLoginGuess?> FindByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(g => g.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersToGuessAsync(CancellationToken ct = default)
    {
        var guesses = DbSet;
        return await DbContext.Set<ApplicationUser>()
            .Where(u => u.YandexLogin == null || u.YandexLogin == "")
            .Where(u => u.DisplayName != "")
            .Where(u => !guesses.Any(g => g.UserId == u.Id && g.Status != YandexLoginGuessStatus.Error))
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);
    }
}
