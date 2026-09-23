namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Пользователь приложения. Самописная сущность (без ASP.NET Identity) — маппится в
/// таблицу <c>app_user</c>, повторяющую схему go-based версии проекта. Права хранятся
/// отдельно в <see cref="UserPermission"/>.
/// </summary>
public class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public long? TgUserId { get; set; }
    public bool IsChatMember { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? YandexLogin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<RoleTitle> PreferredRoles = [];
    public UserPreferences? Preferences;
}
