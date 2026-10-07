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
        var candidates = await DbContext.Set<ApplicationUser>()
            .Where(u => u.YandexLogin == null || u.YandexLogin == "")
            .OrderBy(u => u.CreatedAt)
            .Select(u => new { User = u, Guess = guesses.FirstOrDefault(g => g.UserId == u.Id) })
            .ToListAsync(ct);

        // Имя для поиска вычисляется в памяти: попытка повторяется, если имя изменилось
        // (например, вручную заполнили фамилию и имя)
        return candidates
            .Where(c => c.User.GetFullName() != "")
            .Where(c => c.Guess == null
                        || c.Guess.Status == YandexLoginGuessStatus.Error
                        || c.Guess.Query != c.User.GetFullName())
            .Select(c => c.User)
            .ToList();
    }
}
