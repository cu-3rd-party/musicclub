using CuMusicClub.Application.Auth;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Security;

public interface IAuthService
{
    Task<AuthSessionDto> CreateAuthSession(ApplicationUser user, CancellationToken cancellationToken);

    Task<TokenPairDto> RefreshSession(string refreshToken, CancellationToken cancellationToken);
}
