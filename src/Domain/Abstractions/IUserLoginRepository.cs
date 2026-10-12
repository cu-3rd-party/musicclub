using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

/// <summary>Репозиторий привязок пользователей к внешним провайдерам входа.</summary>
public interface IUserLoginRepository : IRepository<UserLogin>
{
    Task<UserLogin?> FindAsync(string provider, string subject, CancellationToken cancellationToken = default);
}
