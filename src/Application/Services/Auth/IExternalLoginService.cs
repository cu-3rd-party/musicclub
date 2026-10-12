using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Auth;

/// <summary>Вход через внешний провайдер (OIDC / OAuth 2.0).</summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Находит пользователя по привязке (<see cref="ExternalIdentity.Provider"/>, <see cref="ExternalIdentity.Subject"/>),
    /// иначе — по <see cref="ExternalIdentity.TelegramUserId"/>, иначе заводит нового; привязку сохраняет.
    /// </summary>
    Task<ApplicationUser> LoginAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Одноразовый короткоживущий код, по которому SPA заберёт сессию после редиректа от провайдера.</summary>
    string IssueLoginCode(Guid userId);

    /// <summary>Обменивает код из <see cref="IssueLoginCode"/> на сессию; повторно код не сработает.</summary>
    Task<AuthSessionDto?> RedeemLoginCodeAsync(string code, CancellationToken cancellationToken = default);
}
