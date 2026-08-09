using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<ApplicationUser> Users { get; }
    IQueryable<Calendar> Calendars { get; }
    IQueryable<CalendarAttachState> CalendarAttachStates { get; }
    IQueryable<Domain.Entities.Song> Songs { get; }
    IQueryable<SongRole> SongRoles { get; }
    IQueryable<SongRoleAssignment> SongRoleAssignments { get; }
    IQueryable<UserSession> UserSessions { get; }

    void Add(object entity);
    void Remove(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
