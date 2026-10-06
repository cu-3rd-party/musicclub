using CuMusicClub.Application.Services.User;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Заглушка для CalDAV-провайдера: suggest-contacts есть только в веб-API Яндекс.Календаря.
/// </summary>
public sealed class NullYandexEmailSearchService : IYandexEmailSearchService
{
    public bool IsAvailable
    {
        get { return false; }
    }

    public Task<string?> SearchEmailByNameAsync(string query, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }
}
