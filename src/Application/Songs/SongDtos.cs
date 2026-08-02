namespace CuMusicClub.Application.Songs;

public sealed record SongLinkDto(string Kind, string Url);

public sealed record SongDto(
    Guid Id,
    string Title,
    string Artist,
    string? Description,
    SongLinkDto Link,
    string? ThumbnailUrl,
    bool Featured,
    Guid? CreatedById,
    IReadOnlyList<string> AvailableRoles,
    bool EditableByMe,
    int AssignmentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SongUserDto(Guid Id, string DisplayName, string Username, string? AvatarUrl);

public sealed record RoleAssignmentDto(string Role, SongUserDto User, DateTimeOffset JoinedAt);

public sealed record PermissionsDto(
    bool EditOwnParticipation,
    bool EditAnyParticipation,
    bool EditOwnSongs,
    bool EditAnySongs,
    bool EditFeaturedSongs,
    bool EditEvents,
    bool EditTracklists);

public sealed record SongDetailsDto(SongDto Song, IReadOnlyList<RoleAssignmentDto> Assignments, PermissionsDto Permissions);

public sealed record ListSongsResultDto(IReadOnlyList<SongDto> Songs, string? NextPageToken);

public sealed record CreateSongRequest(
    string Title,
    string Artist,
    string? Description,
    SongLinkDto? Link,
    string? ThumbnailUrl,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);

public sealed record UpdateSongRequest(
    string Title,
    string Artist,
    string? Description,
    SongLinkDto? Link,
    string? ThumbnailUrl,
    bool Featured,
    IReadOnlyList<string>? AvailableRoles);
