namespace CuMusicClub.Application.Auth;

public interface ITelegramAuthService
{
    Task<AuthSessionDto> AuthenticateAsync(string initData, CancellationToken cancellationToken);
    Task<TokenPairDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
