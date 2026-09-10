using System.Security.Claims;

namespace CuMusicClub.Application.Services.Roadie;

public enum RoadieAcceptResult
{
    Accepted,
    NotARoadie,
    NotFound,
    AlreadyAccepted
}

public interface IRoadieService
{
    Task<RoadieTicketDto> CreateTicketAsync(Guid songId,
        ApplicationUser currentUser,
        RoadieTicketType ticketType = RoadieTicketType.Help,
        CancellationToken cancellationToken = default);

    Task<RoadieAcceptResult> AcceptTicketAsync(long tgUserId,
        Guid ticketId,
        CancellationToken cancellationToken = default);

    Task<int> AutoAssignOpenTicketsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ApplicationUser>> ListRoadies(CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetRoadie(Domain.Entities.Song song, CancellationToken cancellationToken = default);

    /// <summary>
    /// Назначает роуди на песню (заменяет текущего, если есть). Только пользователь с правом
    /// <c>roadie.manage</c>; цель также должна обладать этим правом.
    /// </summary>
    Task AssignRoadieAsync(Guid songId,
        ApplicationUser targetUser,
        ApplicationUser actor,
        CancellationToken cancellationToken = default);

    /// <summary>Снимает роуди с песни. Только пользователь с правом <c>roadie.manage</c>.</summary>
    Task RemoveRoadieAsync(Guid songId,
        ApplicationUser actor,
        CancellationToken cancellationToken = default);

    /// <summary>Пользователи, которых можно назначить роуди (с правом <c>roadie.manage</c>), с поиском.</summary>
    Task<IReadOnlyList<ApplicationUser>> GetRoadieCandidatesAsync(string? query,
        CancellationToken cancellationToken = default);
}
