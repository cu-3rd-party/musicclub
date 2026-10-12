namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Привязка пользователя к аккаунту у внешнего провайдера входа (OIDC / OAuth 2.0):
/// пара (<see cref="Provider"/>, <see cref="Subject"/>) однозначно определяет пользователя.
/// </summary>
public class UserLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>Имя провайдера из конфига (<c>telegram</c>), в нижнем регистре.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Claim <c>sub</c> провайдера.</summary>
    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public ApplicationUser? User { get; set; }

    public static string NormalizeProvider(string provider)
    {
        return provider.Trim().ToLowerInvariant();
    }
}
