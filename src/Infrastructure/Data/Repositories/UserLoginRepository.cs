using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class UserLoginRepository(DbContext dbContext) : Repository<UserLogin>(dbContext), IUserLoginRepository
{
    public async Task<UserLogin?> FindAsync(string provider,
        string subject,
        CancellationToken cancellationToken = default)
    {
        var normalized = UserLogin.NormalizeProvider(provider);
        return await DbSet.FirstOrDefaultAsync(l => l.Provider == normalized && l.Subject == subject,
            cancellationToken);
    }
}
