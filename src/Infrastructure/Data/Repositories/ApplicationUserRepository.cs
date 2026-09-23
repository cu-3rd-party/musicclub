using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data.Repositories;

public sealed class ApplicationUserRepository(ApplicationDbContext dbContext)
    : Repository<ApplicationUser>(dbContext), IApplicationUserRepository
{
    public async Task<ApplicationUser?> FindByTgUserIdAsync(long tgUserId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.TgUserId == tgUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersWithoutPermissionsAsync(int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .Where(u => !dbContext.UserPermissions.Any(p => p.UserId == u.Id))
            .OrderBy(u => u.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserPermissions
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);
    }

    public async Task GrantPermissionsAsync(Guid userId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var distinct = permissions.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return;

        var existing = await dbContext.UserPermissions
            .Where(p => p.UserId == userId)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var permission in distinct)
        {
            if (existingSet.Contains(permission)) continue;
            await dbContext.UserPermissions.AddAsync(new UserPermission
            {
                UserId = userId,
                Permission = permission,
            }, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersByPermissionAsync(string permission,
        CancellationToken cancellationToken = default)
    {
        var permissionUserIds = dbContext.UserPermissions
            .Where(up => up.Permission == permission)
            .Select(up => up.UserId);

        return await dbContext.Users
            .Where(u => permissionUserIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersAsync(string? query,
        CancellationToken cancellationToken = default)
    {
        var users = dbContext.Users.Include(u => u.Preferences).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowered = query.Trim().ToLowerInvariant();
            users = users.Where(u =>
                u.DisplayName.ToLower().Contains(lowered) ||
                (u.UserName != null && u.UserName.ToLower().Contains(lowered)));
        }

        return await users.OrderBy(u => u.DisplayName).ToListAsync(cancellationToken);
    }

    public async Task<UserPreferences?> GetPreferencesAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserPreferencesEnumerable
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task SetPreferencesAsync(Guid userId,
        bool allowAdding,
        bool allowRemoving,
        CancellationToken cancellationToken = default)
    {
        var prefs = await dbContext.UserPreferencesEnumerable
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (prefs == null)
        {
            prefs = new UserPreferences
            {
                UserId = userId,
                AllowAdding = allowAdding,
                AllowRemoving = allowRemoving,
            };
            await dbContext.UserPreferencesEnumerable.AddAsync(prefs, cancellationToken);
            return;
        }

        prefs.AllowAdding = allowAdding;
        prefs.AllowRemoving = allowRemoving;
    }
    
    public async Task<ApplicationUser?> FindByYandexLoginAsync(string yandexLogin, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.YandexLogin == yandexLogin, cancellationToken);
    }
}