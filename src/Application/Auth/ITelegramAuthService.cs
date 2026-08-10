using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Auth;

public interface ITelegramAuthService
{
    /// <summary>
    /// Проверяет криптографически истинность токена и время его жизни,
    /// </summary>
    /// <param name="initData">Строка, содержащая необработанные данные инициализации</param>
    /// <returns></returns>
    /// <exception cref="BadHttpRequestException">Если функция хочет вернуть false, она поднимает ошибку с описанием, что конкретно не прошло проверку</exception>
    void Validate(string initData);

    /// <summary>
    /// Создает суп из инит данных и достает из супа айди клиента, поднимая ошибки если не получилось
    /// </summary>
    /// <param name="initData">Строка, содержащая необработанные данные инициализации</param>
    /// <returns></returns>
    public Telegram.Bot.Types.User? ExtractTgUser(string initData);

    /// <summary>
    /// Validates initData, upserts user, and issues tokens.
    /// </summary>
    Task<AuthSessionDto> AuthenticateAsync(string initData, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a new token pair.
    /// </summary>
    Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Create a deeplink to auth the user
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TelegramDeeplink> CreateDeeplink(CancellationToken cancellationToken);

    /// <summary>
    /// Получить статус по диплинку
    /// </summary>
    /// <param name="linkUid"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AuthSessionDto?> GetDeeplink(Guid linkUid, CancellationToken cancellationToken);

    Task<ApplicationUser> UpsertUserAsync(Telegram.Bot.Types.User tgUser, CancellationToken cancellationToken);
}
