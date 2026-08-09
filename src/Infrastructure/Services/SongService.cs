using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Song;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Domain.ValueObjects;
using CuMusicClub.Infrastructure.Data;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services;

public class SongService : ISongService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private static readonly Guid RoleIdNamespace = Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    private readonly ApplicationDbContext _db;

    public SongService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ListSongsResultDto> ListAsync(
        string? query, int pageSize, string? pageToken, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var limit = pageSize <= 0 || pageSize > MaxPageSize ? DefaultPageSize : pageSize;
        var offset = int.TryParse(pageToken, out var parsed) && parsed >= 0 ? parsed : 0;

        var songsQuery = _db.Songs
            .Include(s => s.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            songsQuery = songsQuery.Where(s => EF.Functions.ILike(s.Title, pattern) || EF.Functions.ILike(s.Artist, pattern));
        }

        var songs = await songsQuery
            .OrderByDescending(s => s.IsFeatured)
            .ThenByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var permissions = PermissionsFrom(currentUser);
        var songIds = songs.Select(s => s.Id).ToList();

        var rolesBySong = await _db.SongRoles
            .Where(r => songIds.Contains(r.SongId))
            .GroupBy(r => r.SongId)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.OrderBy(r => r.Role).Select(r => r.Role).ToList(),
                cancellationToken);

        var assignmentCounts = await (
            from a in _db.SongRoleAssignments
            join r in _db.SongRoles on new { a.SongId, a.Role } equals new { r.SongId, r.Role }
            where songIds.Contains(a.SongId)
            group a by a.SongId into g
            select new { SongId = g.Key, Count = g.Select(a => a.Role).Distinct().Count() })
            .ToDictionaryAsync(x => x.SongId, x => x.Count, cancellationToken);

        var result = new List<SongDto>(songs.Count);
        foreach (var song in songs)
        {
            rolesBySong.TryGetValue(song.Id, out var roles);
            assignmentCounts.TryGetValue(song.Id, out var count);
            result.Add(ToSongDtoBrief(song, permissions, currentUser, roles ?? [], count));
        }

        var nextPageToken = result.Count == limit ? (offset + limit).ToString() : null;

        return new ListSongsResultDto(result, nextPageToken);
    }

    public async Task<SongDetailsDto> GetAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await _db.Songs
            .Include(s => s.CreatedBy)
            .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
            ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var permissions = PermissionsFrom(currentUser);

        var roles = await _db.SongRoles
            .Where(r => r.SongId == songId)
            .OrderBy(r => r.Role)
            .ToListAsync(cancellationToken);

        var assignmentData = await (
            from a in _db.SongRoleAssignments
            join u in _db.Users on a.UserId equals u.Id
            where a.SongId == songId
            orderby a.JoinedAt
            select new
            {
                a.Role,
                UserDto = new SongUserDto(u.Id, u.DisplayName ?? string.Empty, u.UserName ?? string.Empty, u.AvatarUrl),
                a.JoinedAt,
            })
            .ToListAsync(cancellationToken);

        var assignments = assignmentData
            .Select(a => new RoleAssignmentDto(a.UserDto, a.JoinedAt))
            .ToList();

        // Map assignments to their roles (first assignment per role wins)
        var assignmentByRole = new Dictionary<string, RoleAssignmentDto>();
        foreach (var item in assignmentData)
        {
            if (!assignmentByRole.ContainsKey(item.Role))
            {
                var dto = new RoleAssignmentDto(item.UserDto, item.JoinedAt);
                assignmentByRole[item.Role] = dto;
            }
        }

        var songDto = ToSongDtoFull(song, permissions, currentUser, roles, assignmentByRole, assignments.Count);
        return new SongDetailsDto(songDto, assignments, permissions);
    }

    public async Task<SongDetailsDto> CreateAsync(
        CreateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var permissions = PermissionsFrom(currentUser);

        if (!permissions.EditOwnSongs && !permissions.EditAnySongs)
            throw new ForbiddenAccessException();

        if (request.Featured && !permissions.EditFeaturedSongs)
            throw new ForbiddenAccessException();

        var linkKind = DeriveLinkKind(request.Url);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Url);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

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
        _db.Songs.Add(song);
        await _db.SaveChangesAsync(cancellationToken);

        await ReplaceRolesAsync(song.Id, NormalizeRoles(request.AvailableRoles), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(song.Id, currentUser, cancellationToken);
    }

    public async Task<SongDetailsDto> UpdateAsync(
        Guid songId, UpdateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
            ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var permissions = PermissionsFrom(currentUser);

        if (!PermissionAllowsSongEdit(permissions, song.CreatedById, currentUser))
            throw new ForbiddenAccessException();

        var featuredAllowed = permissions.EditFeaturedSongs;
        if (request.Featured && !featuredAllowed)
            throw new ForbiddenAccessException();

        var linkKind = DeriveLinkKind(request.Url);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Url);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        song.Title = request.Title;
        song.Artist = request.Artist;
        song.Description = request.Description;
        song.LinkKind = linkKind;
        song.LinkUrl = request.Url;
        song.ThumbnailUrl = thumbnailUrl;
        if (featuredAllowed)
            song.IsFeatured = request.Featured;
        song.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var requestedRoles = NormalizeRoles(request.AvailableRoles);
        var currentRoles = await _db.SongRoles
            .Where(r => r.SongId == songId)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);
        currentRoles.Sort(StringComparer.Ordinal);

        if (!requestedRoles.SequenceEqual(currentRoles, StringComparer.Ordinal))
        {
            await ReplaceRolesAsync(songId, requestedRoles, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(songId, currentUser, cancellationToken);
    }

    public async Task DeleteAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
            ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var permissions = PermissionsFrom(currentUser);

        if (!PermissionAllowsSongEdit(permissions, song.CreatedById, currentUser))
            throw new ForbiddenAccessException();

        _db.Songs.Remove(song);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SongDetailsDto> JoinRoleAsync(
        Guid songId, string role, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var permissions = PermissionsFrom(currentUser);

        if (!permissions.EditAnyParticipation && !permissions.EditOwnParticipation)
            throw new ForbiddenAccessException();

        var songExists = await _db.Songs.AnyAsync(s => s.Id == songId, cancellationToken);
        if (!songExists)
            throw new NotFoundException(songId.ToString(), nameof(Song));

        var roleExists = await _db.SongRoles.AnyAsync(r => r.SongId == songId && r.Role == role, cancellationToken);
        if (!roleExists)
            throw new NotFoundException(role, nameof(SongRole));

        var userId = currentUser.GetUserId();
        var alreadyJoined = await _db.SongRoleAssignments.AnyAsync(
            a => a.SongId == songId && a.Role == role && a.UserId == userId, cancellationToken);

        if (!alreadyJoined)
        {
            _db.SongRoleAssignments.Add(new SongRoleAssignment
            {
                SongId = songId,
                Role = role,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(songId, currentUser, cancellationToken);
    }

    public async Task<SongDetailsDto> LeaveRoleAsync(
        Guid songId, string role, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var permissions = PermissionsFrom(currentUser);

        if (!permissions.EditAnyParticipation && !permissions.EditOwnParticipation)
            throw new ForbiddenAccessException();

        var songExists = await _db.Songs.AnyAsync(s => s.Id == songId, cancellationToken);
        if (!songExists)
            throw new NotFoundException(songId.ToString(), nameof(Song));

        var userId = currentUser.GetUserId();

        await _db.SongRoleAssignments
            .Where(a => a.SongId == songId && a.Role == role && a.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetAsync(songId, currentUser, cancellationToken);
    }

    // --- DTO mapping ---

    private SongDto ToSongDtoBrief(
        Song song, PermissionsDto permissions, ClaimsPrincipal currentUser, IReadOnlyList<string> roles, int assignmentCount)
    {
        return new SongDto(
            song.Id,
            song.Title,
            song.Artist,
            song.Description,
            song.LinkUrl,
            song.ThumbnailUrl,
            song.IsFeatured,
            MapCreatedBy(song.CreatedBy),
            roles.Select(r => MakeRoleDto(r)).ToList(),
            PermissionAllowsSongEdit(permissions, song.CreatedById, currentUser),
            assignmentCount,
            song.CreatedAt,
            song.UpdatedAt);
    }

    private SongDto ToSongDtoFull(
        Song song, PermissionsDto permissions, ClaimsPrincipal currentUser,
        IReadOnlyList<SongRole> roles, Dictionary<string, RoleAssignmentDto> assignmentByRole, int assignmentCount)
    {
        var roleDtos = roles.Select(r =>
        {
            assignmentByRole.TryGetValue(r.Role, out var assignment);
            return MakeRoleDto(r.Role, assignment);
        }).ToList();

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
            PermissionAllowsSongEdit(permissions, song.CreatedById, currentUser),
            assignmentCount,
            song.CreatedAt,
            song.UpdatedAt);
    }

    private static SongUserDto MapCreatedBy(ApplicationUser? user)
    {
        if (user is null)
            return new SongUserDto(Guid.Empty, "Unknown", "unknown", null);

        return new SongUserDto(user.Id, user.DisplayName ?? string.Empty, user.UserName ?? string.Empty, user.AvatarUrl);
    }

    private static RoleDto MakeRoleDto(string roleName, RoleAssignmentDto? assignment = null)
    {
        return new RoleDto(MakeRoleId(roleName), roleName, assignment);
    }

    private static Guid MakeRoleId(string roleName)
    {
        var nameBytes = RoleIdNamespace.ToByteArray()
            .Concat(Encoding.UTF8.GetBytes(roleName))
            .ToArray();
        var hash = MD5.HashData(nameBytes);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }

    // --- Permissions ---

    public static PermissionsDto PermissionsFrom(ClaimsPrincipal user)
    {
        return new PermissionsDto(
            user.HasPermission(Permissions.ParticipationEditOwn),
            user.HasPermission(Permissions.ParticipationEditAny),
            user.HasPermission(Permissions.SongsEditOwn),
            user.HasPermission(Permissions.SongsEditAny),
            user.HasPermission(Permissions.SongsEditFeatured),
            user.HasPermission(Permissions.EventsEdit),
            user.HasPermission(Permissions.TracklistsEdit));
    }

    // --- Role management ---

    private async Task ReplaceRolesAsync(Guid songId, IReadOnlyCollection<string> desiredRoles, CancellationToken cancellationToken)
    {
        var currentRoles = await _db.SongRoles
            .Where(r => r.SongId == songId)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);

        var desiredSet = desiredRoles.ToHashSet(StringComparer.Ordinal);

        var toRemove = currentRoles.Where(role => !desiredSet.Contains(role)).ToList();
        if (toRemove.Count > 0)
        {
            await _db.SongRoles
                .Where(r => r.SongId == songId && toRemove.Contains(r.Role))
                .ExecuteDeleteAsync(cancellationToken);
        }

        foreach (var role in desiredSet.Where(role => !currentRoles.Contains(role)))
        {
            _db.SongRoles.Add(new SongRole { SongId = songId, Role = role });
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

    // --- Link handling ---

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

    // --- Authorization ---

    private static bool PermissionAllowsSongEdit(PermissionsDto permissions, Guid? ownerId, ClaimsPrincipal currentUser)
    {
        return permissions.EditAnySongs || (permissions.EditOwnSongs && ownerId == currentUser.GetUserId());
    }
}
