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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SongUserDto(Guid Id, string DisplayName, string? Username, string? AvatarUrl);

public sealed record RoleAssignmentDto(Guid Id, SongUserDto User, DateTimeOffset JoinedAt);

public sealed record RoleDto(Guid Id, string Title, RoleAssignmentDto? Assignment);

public sealed record PermissionsDto(
    bool EditOwnParticipation,
    bool EditAnyParticipation,
    bool EditOwnSongs,
    bool EditAnySongs,
    bool EditFeaturedSongs,
    bool EditEvents,
    bool EditTracklists);

public sealed record ListSongsResultDto(IReadOnlyList<SongDto> Songs, string? NextPageToken);

public sealed record CreateSongRequest(
    string Title,
    string Artist,
    string? Description,
    string Url,
    string? ThumbnailUrl,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);

public sealed record UpdateSongRequest(
    string Title,
    string Artist,
    string? Description,
    string Url,
    string? ThumbnailUrl,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);
