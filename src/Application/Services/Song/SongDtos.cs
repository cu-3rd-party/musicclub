namespace CuMusicClub.Application.Services.Song;

public sealed record SongDto(
    Guid Id,
    string Title,
    string Artist,
    string? Description,
    string Url,
    string? ThumbnailUrl,
    bool Featured,
    SongUserDto CreatedBy,
    IReadOnlyList<RoleDto> Roles,
    SongUserDto? Roadie,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public bool IsFull
    {
        get
        {
            return Roles.All(r => r.Assignment != null);
        }
    }
}

public sealed record SongUserDto(Guid Id, string DisplayName, string? UserName, string? AvatarUrl, long? TgUserId = null);

public sealed record RoleAssignmentDto(Guid Id, SongUserDto User, DateTimeOffset JoinedAt);

/// <summary>
/// Оч используется в эндпоинте /users/me/assignments ибо там инфу о текущем юзере возвращать не надо, а вернуть инфу
/// о песне надо ибо их много разных
/// </summary>
/// <param name="Id"></param>
/// <param name="Song"></param>
/// <param name="Title"></param>
/// <param name="JoinedAt"></param>
public sealed record SongRoleAssignment(Guid RoleAssignmentId, ShortSongDto Song, string Title, DateTimeOffset JoinedAt);

/// <summary>
/// тоже самое, используется по сути только в /users/me/assignments ибо там полная инфа о песне не нужна совсем
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Artist"></param>
public sealed record ShortSongDto(Guid Id, string Title, string Artist, string? ThumbnailUrl);

public sealed record RoleDto(Guid Id, string Title, RoleAssignmentDto? Assignment);

public sealed record PermissionsDto(
    bool EditOwnParticipation,
    bool EditAnyParticipation,
    bool EditOwnSongs,
    bool EditAnySongs,
    bool EditFeaturedSongs,
    bool EditEvents,
    bool EditTracklists);

public sealed record RoleCandidatesDto(IReadOnlyList<SongUserDto> Users);

public sealed record ListSongsResultDto(IReadOnlyList<SongDto> Songs, string? NextPageToken);

public sealed record CreateSongRequest(
    string Title,
    string Artist,
    string? Description,
    string Url,
    Guid? ThumbnailDataEntryId,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);

public sealed record UpdateSongRequest(
    string Title,
    string Artist,
    string? Description,
    string Url,
    Guid? ThumbnailDataEntryId,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);
