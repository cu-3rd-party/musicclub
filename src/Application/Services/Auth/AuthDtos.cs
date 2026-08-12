namespace CuMusicClub.Application.Services.Auth;

public sealed record TelegramAuthRequest(string InitData);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string Username,
    string? AvatarUrl,
    IEnumerable<string> Permissions,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TokenPairDto(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public sealed record AuthSessionDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset AccessTokenAcquiredAt,
    UserProfileDto User);
