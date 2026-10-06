namespace CuMusicClub.Application.Services.User;

/// <summary>
/// Searches for Yandex emails by user name/surname using Yandex Calendar's suggest-contacts API.
/// Mirrors musicscheduler's search_email_by_name functionality.
/// </summary>
public interface IYandexEmailSearchService
{
    /// <summary>
    /// Whether the search can be performed at all (web calendar provider with configured auth).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Searches for a Yandex email by "Фамилия Имя" format.
    /// Returns email if found unambiguously, null otherwise.
    /// Throws if Yandex could not be queried (auth/network errors).
    /// </summary>
    Task<string?> SearchEmailByNameAsync(string query, CancellationToken ct = default);
}
