using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Songs;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Infrastructure.Data;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services;

public class SongService : ISongService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

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

        var songsQuery = _db.Songs.AsQueryable();

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

        var result = new List<SongDto>(songs.Count);
        foreach (var song in songs)
        {
            result.Add(await ToSongDtoAsync(song, permissions, currentUser, cancellationToken));
        }

        var nextPageToken = result.Count == limit ? (offset + limit).ToString() : null;

        return new ListSongsResultDto(result, nextPageToken);
    }

    public async Task<SongDetailsDto> GetAsync(Guid songId, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId, cancellationToken)
            ?? throw new NotFoundException(songId.ToString(), nameof(Song));

        var permissions = PermissionsFrom(currentUser);

        var assignments = await (
            from assignment in _db.SongRoleAssignments
            join user in _db.Users on assignment.UserId equals user.Id
            where assignment.SongId == songId
            orderby assignment.JoinedAt
            select new RoleAssignmentDto(
                assignment.Role,
                new SongUserDto(user.Id, user.DisplayName, user.UserName ?? string.Empty, user.AvatarUrl),
                assignment.JoinedAt))
            .ToListAsync(cancellationToken);

        var roles = await _db.SongRoles
            .Where(r => r.SongId == songId)
            .OrderBy(r => r.Role)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);

        var songDto = ToSongDto(song, permissions, currentUser, roles, assignments.Count);

        return new SongDetailsDto(songDto, assignments, permissions);
    }

    public async Task<SongDetailsDto> CreateAsync(
        CreateSongRequest request, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var permissions = PermissionsFrom(currentUser);

        if (!permissions.EditOwnSongs && !permissions.EditAnySongs)
        {
            throw new ForbiddenAccessException();
        }

        if (request.Featured && !permissions.EditFeaturedSongs)
        {
            throw new ForbiddenAccessException();
        }

        var linkKind = MapLinkKind(request.Link?.Kind);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Link?.Url);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var song = new Song
        {
            Title = request.Title,
            Artist = request.Artist,
            Description = request.Description,
            LinkKind = linkKind,
            LinkUrl = request.Link?.Url ?? string.Empty,
            CreatedById = currentUser.GetUserId(),
            ThumbnailUrl = thumbnailUrl,
            IsFeatured = request.Featured,
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
        {
            throw new ForbiddenAccessException();
        }

        var featuredAllowed = permissions.EditFeaturedSongs;
        if (request.Featured && !featuredAllowed)
        {
            throw new ForbiddenAccessException();
        }

        var linkKind = MapLinkKind(request.Link?.Kind);
        var thumbnailUrl = SongThumbnail.Normalize(request.ThumbnailUrl, linkKind, request.Link?.Url);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        song.Title = request.Title;
        song.Artist = request.Artist;
        song.Description = request.Description;
        song.LinkKind = linkKind;
        song.LinkUrl = request.Link?.Url ?? string.Empty;
        song.ThumbnailUrl = thumbnailUrl;
        if (featuredAllowed)
        {
            song.IsFeatured = request.Featured;
        }
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
        {
            throw new ForbiddenAccessException();
        }

        _db.Songs.Remove(song);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SongDetailsDto> JoinRoleAsync(
        Guid songId, string role, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var permissions = PermissionsFrom(currentUser);

        if (!permissions.EditAnyParticipation && !permissions.EditOwnParticipation)
        {
            throw new ForbiddenAccessException();
        }

        var songExists = await _db.Songs.AnyAsync(s => s.Id == songId, cancellationToken);
        if (!songExists)
        {
            throw new NotFoundException(songId.ToString(), nameof(Song));
        }

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
        {
            throw new ForbiddenAccessException();
        }

        var songExists = await _db.Songs.AnyAsync(s => s.Id == songId, cancellationToken);
        if (!songExists)
        {
            throw new NotFoundException(songId.ToString(), nameof(Song));
        }

        var userId = currentUser.GetUserId();

        await _db.SongRoleAssignments
            .Where(a => a.SongId == songId && a.Role == role && a.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return await GetAsync(songId, currentUser, cancellationToken);
    }

    private async Task<SongDto> ToSongDtoAsync(
        Song song, PermissionsDto permissions, ClaimsPrincipal currentUser, CancellationToken cancellationToken)
    {
        var roles = await _db.SongRoles
            .Where(r => r.SongId == song.Id)
            .OrderBy(r => r.Role)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);

        var assignmentCount = await (
            from assignment in _db.SongRoleAssignments
            join role in _db.SongRoles on new { assignment.SongId, assignment.Role } equals new { role.SongId, role.Role }
            where assignment.SongId == song.Id
            select assignment.Role).Distinct().CountAsync(cancellationToken);

        return ToSongDto(song, permissions, currentUser, roles, assignmentCount);
    }

    private static SongDto ToSongDto(
        Song song, PermissionsDto permissions, ClaimsPrincipal currentUser, IReadOnlyList<string> roles, int assignmentCount)
    {
        return new SongDto(
            song.Id,
            song.Title,
            song.Artist,
            song.Description,
            new SongLinkDto(LinkKindToString(song.LinkKind), song.LinkUrl),
            song.ThumbnailUrl,
            song.IsFeatured,
            song.CreatedById,
            roles,
            PermissionAllowsSongEdit(permissions, song.CreatedById, currentUser),
            assignmentCount,
            song.CreatedAt,
            song.UpdatedAt);
    }

    private static PermissionsDto PermissionsFrom(ClaimsPrincipal user)
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

    private static string LinkKindToString(SongLinkType linkKind)
    {
        return linkKind switch
        {
            SongLinkType.Youtube => "youtube",
            SongLinkType.YandexMusic => "yandex_music",
            SongLinkType.Soundcloud => "soundcloud",
            _ => "unknown",
        };
    }

    private static SongLinkType MapLinkKind(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            "youtube" => SongLinkType.Youtube,
            "yandex_music" => SongLinkType.YandexMusic,
            "soundcloud" => SongLinkType.Soundcloud,
            _ => throw new ValidationException([new ValidationFailure("link.kind", $"Unsupported song link kind: {kind}")]),
        };
    }

    private static bool PermissionAllowsSongEdit(PermissionsDto permissions, Guid? ownerId, ClaimsPrincipal currentUser)
    {
        return permissions.EditAnySongs || (permissions.EditOwnSongs && ownerId == currentUser.GetUserId());
    }
}
