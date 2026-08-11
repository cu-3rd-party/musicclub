using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Song;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Domain.ValueObjects;
using CuMusicClub.Infrastructure.Data;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Services;

public class SongService(
    ILogger<SongService> logger,
    IPermissionService permissionService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager
) : ISongService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<ListSongsResultDto> ListAsync(string? query, int pageSize, string? pageToken, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var limit = pageSize <= 0 || pageSize > MaxPageSize ? DefaultPageSize : pageSize;
        var offset = int.TryParse(pageToken, out var parsed) && parsed >= 0 ? parsed : 0;

        var songsQuery = db.Songs
            .Include(s => s.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            songsQuery = songsQuery.Where(s =>
                EF.Functions.ILike(s.Title, pattern) || EF.Functions.ILike(s.Artist, pattern));
        }

        var songs = await songsQuery
            .Include(s => s.Roles)
            .ThenInclude(r => r.Assignment)
            .ThenInclude(a => a!.User)
            .OrderByDescending(s => s.IsFeatured)
            .ThenByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var result = songs
            .Select(song => ToSongDto(song, song.Roles))
            .ToList();

        // TODO: make next page token more robust
        var nextPageToken = result.Count == limit ? (offset + limit).ToString() : null;

        return new ListSongsResultDto(result, nextPageToken);
    }

    public async Task<SongDto> GetAsync(Guid songId, CancellationToken cancellationToken)
    {
        var song = await db.Songs.AsNoTracking()
                       .Include(s => s.CreatedBy)
                       .Include(s => s.Roles)
                       .ThenInclude(r => r.Assignment)
                       .ThenInclude(a => a!.User)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
                   ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var songDto = ToSongDto(song, song.Roles);

        return songDto;
    }

    public async Task<SongDto> CreateAsync(CreateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);
        if (!permissions.Contains(Permissions.ParticipationEditOwn))
            throw new ForbiddenAccessException();

        if (request.Featured && !permissions.Contains(Permissions.SongsEditFeatured))
            throw new ForbiddenAccessException();

        var linkKind = DeriveLinkKind(request.Url);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Url);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var song = new Song
        {
            Title = request.Title,
            Artist = request.Artist,
            Description = request.Description,
            LinkKind = linkKind,
            LinkUrl = request.Url,
            CreatedById = currentUser.GetUserId(),
            ThumbnailUrl = thumbnailUrl,
            IsFeatured = request.Featured,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Songs.Add(song);
        await db.SaveChangesAsync(cancellationToken);

        await ReplaceRolesAsync(song.Id, NormalizeRoles(request.AvailableRoles), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
    }

    public async Task<SongDto> UpdateAsync(Guid songId, UpdateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await db.Songs
                       .Include(s => s.CreatedBy)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
                   ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        if (song.CreatedBy != null && user != song.CreatedBy && !permissions.Contains(Permissions.SongsEditAny))
            throw new ForbiddenAccessException();

        if (request.Featured && !permissions.Contains(Permissions.SongsEditFeatured))
            throw new ForbiddenAccessException();

        var linkKind = DeriveLinkKind(request.Url);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Url);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        song.Title = request.Title;
        song.Artist = request.Artist;
        song.Description = request.Description;
        song.LinkKind = linkKind;
        song.LinkUrl = request.Url;
        song.ThumbnailUrl = thumbnailUrl;
        if (permissions.Contains(Permissions.SongsEditFeatured))
            song.IsFeatured = request.Featured;
        song.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var requestedRoles = NormalizeRoles(request.AvailableRoles);
        var currentRoles = await db.SongRoles
            .Where(r => r.SongId == songId)
            .Select(r => r.RoleTitle)
            .ToListAsync(cancellationToken);
        currentRoles.Sort(StringComparer.Ordinal);

        if (!requestedRoles.SequenceEqual(currentRoles, StringComparer.Ordinal))
        {
            await ReplaceRolesAsync(songId, requestedRoles, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(songId, cancellationToken);
    }

    public async Task DeleteAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await db.Songs
                       .Include(s => s.CreatedBy)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
                   ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);
        if (song.CreatedBy != null && user != song.CreatedBy && !permissions.Contains(Permissions.SongsEditAny))
            throw new ForbiddenAccessException();

        db.Songs.Remove(song);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SongDto> JoinRoleAsync(ApplicationUser user, ClaimsPrincipal claimsPrincipal, Guid roleId, CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        var requester = await userManager.GetUserAsync(claimsPrincipal) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;
        if ((isSelf && !permissions.Contains(Permissions.ParticipationEditOwn)) || (!isSelf && !permissions.Contains(Permissions.ParticipationEditAny)))
            throw new ForbiddenAccessException();
        
        var role = await db.SongRoles
            .Include(r => r.Song)
            .Include(r => r.Assignment)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (role == null )
            throw new NotFoundException(roleId.ToString(), nameof(SongRole));
        
        if (role.Assignment != null)
            throw new BadHttpRequestException("the role is already occupied");

        role.Assignment = new SongRoleAssignment { UserId = user.Id, SongId = role.SongId, RoleId = role.Id };
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(role.Song.Id, cancellationToken);
    }

    public async Task<SongDto> LeaveRoleAsync(ApplicationUser user, ClaimsPrincipal claimsPrincipal, Guid roleId, CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        var requester = await userManager.GetUserAsync(claimsPrincipal) ?? throw new UnauthorizedAccessException();
        var isSelf = requester.Id == user.Id;
        if ((isSelf && !permissions.Contains(Permissions.ParticipationEditOwn)) || (!isSelf && !permissions.Contains(Permissions.ParticipationEditAny)))
            throw new ForbiddenAccessException();
        
        var role = await db.SongRoles
            .Include(r => r.Song)
            .Include(r => r.Assignment)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
        if (role == null )
            throw new NotFoundException(roleId.ToString(), nameof(SongRole));

        if (role.Assignment == null)
            throw new BadHttpRequestException("role is unoccupied");

        await db.SongRoleAssignments
            .Where(s => s.Id == role.Assignment.Id)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetAsync(role.Song.Id, cancellationToken);
    }

    #region DTO mapping

    private SongDto ToSongDto(Song song, IReadOnlyList<SongRole> roles)
    {
        var roleDtos = roles.Select(r => new RoleDto(r.Id,
            r.RoleTitle,
            r.Assignment is null
                ? null
                : new RoleAssignmentDto(r.Assignment.Id,
                    new SongUserDto(r.Assignment.User.Id,
                        r.Assignment.User.DisplayName,
                        r.Assignment.User.UserName,
                        r.Assignment.User.AvatarUrl),
                    r.Assignment.JoinedAt))).ToList();

        return new SongDto(
            song.Id,
            song.Title,
            song.Artist,
            song.Description,
            song.LinkUrl,
            song.ThumbnailUrl,
            song.IsFeatured,
            MapCreatedBy(song.CreatedBy),
            roleDtos,
            song.CreatedAt,
            song.UpdatedAt);
    }

    private static SongUserDto MapCreatedBy(ApplicationUser? user)
    {
        if (user is null)
            return new SongUserDto(Guid.Empty, "Unknown", "unknown", null);

        return new SongUserDto(user.Id, user.DisplayName ?? string.Empty, user.UserName ?? string.Empty,
            user.AvatarUrl);
    }

    #endregion

    #region Role management

    private async Task ReplaceRolesAsync(Guid songId, IReadOnlyCollection<string> desiredRoles,
        CancellationToken cancellationToken)
    {
        var currentRoles = await db.SongRoles
            .Where(r => r.SongId == songId)
            .Select(r => r.RoleTitle)
            .ToListAsync(cancellationToken);

        var desiredSet = desiredRoles.ToHashSet(StringComparer.Ordinal);

        var toRemove = currentRoles.Where(role => !desiredSet.Contains(role)).ToList();
        if (toRemove.Count > 0)
        {
            await db.SongRoles
                .Where(r => r.SongId == songId && toRemove.Contains(r.RoleTitle))
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (var role in desiredSet.Where(role => !currentRoles.Contains(role)))
        {
            db.SongRoles.Add(new SongRole { SongId = songId, RoleTitle = role });
        }
    }

    private static List<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        return roles?
            .Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList() ?? [];
    }

    #endregion

    #region Link handling

    private static SongLinkType DeriveLinkKind(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ValidationException([new ValidationFailure("url", "Song URL is required")]);

        var lower = url.Trim().ToLowerInvariant();

        if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
            return SongLinkType.Youtube;

        if (lower.Contains("music.yandex") || lower.Contains("yandex.ru"))
            return SongLinkType.YandexMusic;

        if (lower.Contains("soundcloud.com"))
            return SongLinkType.Soundcloud;

        throw new ValidationException([new ValidationFailure("url", $"Unsupported song link URL: {url}")]);
    }

    #endregion
}
