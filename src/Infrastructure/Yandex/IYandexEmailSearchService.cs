namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Searches for Yandex emails by user name/surname using Yandex Calendar's suggest-contacts API.
/// Mirrors musicscheduler's search_email_by_name functionality.
/// </summary>
public interface IYandexEmailSearchService
{
    /// <summary>
    /// Searches for a Yandex email by "Фамилия Имя" format.
    /// Returns email if found, null otherwise.
    /// </summary>
    Task<string?> SearchEmailByNameAsync(string query, CancellationToken ct = default);
}
