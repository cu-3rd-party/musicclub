namespace CuMusicClub.Application.Services.Auth;

/// <summary>Пользователь внешнего провайдера входа — провайдер-независимо, из стандартных OIDC-клеймов.</summary>
/// <param name="Provider">Имя провайдера из конфига (<c>telegram</c>).</param>
/// <param name="Subject">Claim <c>sub</c>.</param>
/// <param name="DisplayName">Claim <c>name</c> (или <c>given_name</c> + <c>family_name</c>).</param>
/// <param name="Username">Claim <c>preferred_username</c>.</param>
/// <param name="PictureUrl">Claim <c>picture</c>.</param>
/// <param name="TelegramUserId">
/// Id пользователя в Telegram, если провайдер его отдаёт (см. <c>TelegramUserIdClaim</c> в конфиге провайдера):
/// по нему находится аккаунт, заведённый через бота или Mini App, и к нему привязываются уведомления бота.
/// </param>
public sealed record ExternalIdentity(
    string Provider,
    string Subject,
    string? DisplayName,
    string? Username,
    string? PictureUrl = null,
    long? TelegramUserId = null);
