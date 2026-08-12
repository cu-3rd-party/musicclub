namespace CuMusicClub.Domain.Constants;

public static class Permission
{
    public const string ParticipationEditOwn = "participation.edit_own";
    public const string ParticipationEditAny = "participation.edit_any";
    public const string SongsEditOwn = "songs.edit_own";
    public const string SongsEditAny = "songs.edit_any";
    public const string SongsEditFeatured = "songs.edit_featured";
    public const string EventsEdit = "events.edit";
    public const string TracklistsEdit = "tracklists.edit";

    public static readonly IReadOnlyList<string> Default = [ParticipationEditOwn, SongsEditOwn,];

    public static readonly IReadOnlyList<string> Roadie = [ParticipationEditOwn, ParticipationEditAny, SongsEditOwn,];

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

    /// <summary>
    /// Role name → permission bundle. Roles are pure sugar: they carry no behaviour of their
    /// own. Assigning a role to a user merely materializes <see cref="ByRole"/>[role] as
    /// individual <c>permission</c> claims on that user.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.Administrator] = All,
            [Roles.Roadie] = Roadie,
            [Roles.Default] = Default,
        };
}