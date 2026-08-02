namespace CuMusicClub.Application.Common.Auth;

/// <summary>
/// Claim type that carries granular permissions (e.g. <c>"permission"</c> with value
/// <c>songs.edit_own</c>). Permissions can be granted on users or on roles.
/// </summary>
public static class PermissionClaimTypes
{
    public const string Permission = "permission";
}

public static class Permissions
{
    public const string ParticipationEditOwn = "participation.edit_own";
    public const string ParticipationEditAny = "participation.edit_any";
    public const string SongsEditOwn = "songs.edit_own";
    public const string SongsEditAny = "songs.edit_any";
    public const string SongsEditFeatured = "songs.edit_featured";
    public const string EventsEdit = "events.edit";
    public const string TracklistsEdit = "tracklists.edit";

    public static readonly IReadOnlyList<string> All =
    [
        ParticipationEditOwn,
        ParticipationEditAny,
        SongsEditOwn,
        SongsEditAny,
        SongsEditFeatured,
        EventsEdit,
        TracklistsEdit,
    ];
}
