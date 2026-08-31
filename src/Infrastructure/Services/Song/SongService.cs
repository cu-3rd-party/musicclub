using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Song.Helpers;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.ValueObjects;
using CuMusicClub.Infrastructure.Data;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.Song;

public partial class SongService(
    IPermissionService permissionService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ITelegramChatService telegramChatService) : ISongService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<ListSongsResultDto> ListAsync(string? query,
        int pageSize,
        string? pageToken,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken)
    {
        var limit = pageSize <= 0 || pageSize > MaxPageSize ? DefaultPageSize : pageSize;
        var offset = int.TryParse(pageToken, out var parsed) && parsed >= 0 ? parsed : 0;

        var songsQuery = db
            .Songs.Include(s => s.CreatedBy)
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
        var song = await db
                       .Songs.AsNoTracking()
                       .Include(s => s.CreatedBy)
                       .Include(s => s.Roles)
                       .ThenInclude(r => r.Assignment)
                       .ThenInclude(a => a!.User)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Domain.Entities.Song));

        var songDto = ToSongDto(song, song.Roles);

        return songDto;
    }

    public async Task<SongDto> CreateAsync(CreateSongRequest request,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);
        if (!permissions.Contains(Domain.Constants.Permission.ParticipationEditOwn))
            throw new ForbiddenAccessException();

        if (request.Featured && !permissions.Contains(Domain.Constants.Permission.SongsEditFeatured))
            throw new ForbiddenAccessException();

        var linkKind = SongHelpers.DeriveLinkKind(request.Url);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (request.ThumbnailDataEntryId.HasValue &&
            await db.DataEntries.FirstOrDefaultAsync(d => d.Id == request.ThumbnailDataEntryId, cancellationToken) ==
            null)
            throw new ValidationException([new ValidationFailure("thumbnailDataEntryId", "Referenced data entry not found")]);

        var song = new Domain.Entities.Song
        {
            Title = request.Title,
            Artist = request.Artist,
            Description = request.Description,
            LinkKind = linkKind,
            LinkUrl = request.Url,
            CreatedById = currentUser.GetUserId(),
            ThumbnailUrl = request.ThumbnailDataEntryId.HasValue
                ? $"/data/{request.ThumbnailDataEntryId}"
                : null,
            ThumbnailDataEntryId = request.ThumbnailDataEntryId,
            IsFeatured = request.Featured,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Songs.Add(song);
        await db.SaveChangesAsync(cancellationToken);

        await ReplaceRolesAsync(song.Id, SongHelpers.NormalizeRoles(request.AvailableRoles), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Отправить объявление в общий чат
        var message = SongServiceFormatter.BuildSongCreatedMessage(song.Title, song.Artist, song.LinkUrl, song.CreatedBy);
        if (!string.IsNullOrEmpty(message)) await telegramChatService.SendGeneralMessage(message, cancellationToken);

        return await GetAsync(song.Id, cancellationToken);
    }

    public async Task<SongDto> UpdateAsync(Guid songId,
        UpdateSongRequest request,
        ClaimsPrincipal currentUser,
        CancellationToken cancellationToken)
    {
        var song = await db
                       .Songs.Include(s => s.CreatedBy)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Domain.Entities.Song));

        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);

        if (song.CreatedBy != null &&
            user != song.CreatedBy &&
            !permissions.Contains(Domain.Constants.Permission.SongsEditAny))
            throw new ForbiddenAccessException();

        if (request.Featured && !permissions.Contains(Domain.Constants.Permission.SongsEditFeatured))
            throw new ForbiddenAccessException();

        var linkKind = SongHelpers.DeriveLinkKind(request.Url);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (request.ThumbnailDataEntryId.HasValue &&
            await db.DataEntries.FirstOrDefaultAsync(d => d.Id == request.ThumbnailDataEntryId, cancellationToken) ==
            null)
            throw new ValidationException([new ValidationFailure("thumbnailDataEntryId", "Referenced data entry not found")]);

        song.Title = request.Title;
        song.Artist = request.Artist;
        song.Description = request.Description;
        song.LinkKind = linkKind;
        song.LinkUrl = request.Url;
        song.ThumbnailUrl =
            $"/data/{request.ThumbnailDataEntryId}"; // Да, это захардкоженный путь. Да, он заставляет ходить фронт к беку и обратно. И что ты мне сделаешь?
        song.ThumbnailDataEntryId = request.ThumbnailDataEntryId;
        if (!request.ThumbnailDataEntryId.HasValue) song.ThumbnailUrl = null;
        if (permissions.Contains(Domain.Constants.Permission.SongsEditFeatured)) song.IsFeatured = request.Featured;
        song.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var requestedRoles = SongHelpers.NormalizeRoles(request.AvailableRoles);
        var currentRoles = await db
            .SongRoles.Where(r => r.SongId == songId)
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
        var song = await db
                       .Songs.Include(s => s.CreatedBy)
                       .FirstOrDefaultAsync(s => s.Id == songId, cancellationToken) ??
                   throw new NotFoundException(songId.ToString(), nameof(Domain.Entities.Song));

        var user = await userManager.GetUserAsync(currentUser) ?? throw new ForbiddenAccessException();
        var permissions = await permissionService.GetPermissionValuesAsync(user, cancellationToken);
        if (song.CreatedBy != null &&
            user != song.CreatedBy &&
            !permissions.Contains(Domain.Constants.Permission.SongsEditAny))
            throw new ForbiddenAccessException();

        db.Songs.Remove(song);
        await db.SaveChangesAsync(cancellationToken);
    }
}
