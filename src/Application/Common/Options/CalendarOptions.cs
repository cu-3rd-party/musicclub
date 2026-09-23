namespace CuMusicClub.Application.Common.Options;

/// <summary>
/// Опции для работы с ICS-календарями.
/// </summary>
public class CalendarOptions
{
    /// <summary>
    /// Базовый URL для публичных ICS-ссылок (например, https://musicclub.ru).
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Время кэширования ICS-фидов в минутах (по умолчанию 5).
    /// </summary>
    public int DefaultCacheMinutes { get; set; } = 5;

    /// <summary>
    /// Срок жизни токена до перевыпуска в днях (по умолчанию 365).
    /// </summary>
    public int TokenValidityDays { get; set; } = 365;
}
