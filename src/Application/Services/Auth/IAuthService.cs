using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthSessionDto> CreateAuthSession(ApplicationUser user, CancellationToken cancellationToken);

    Task<TokenPairDto> RefreshSession(string refreshToken, CancellationToken cancellationToken);
}
