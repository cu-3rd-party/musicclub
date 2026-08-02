using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<ApplicationUser> Users { get; }
    IQueryable<Song> Songs { get; }
    IQueryable<SongRole> SongRoles { get; }
    IQueryable<SongRoleAssignment> SongRoleAssignments { get; }
    IQueryable<Event> Events { get; }
    IQueryable<EventTrackItem> EventTrackItems { get; }
    IQueryable<EventParticipant> EventParticipants { get; }
    IQueryable<TgAuthUser> TgAuthUsers { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<SongTopic> SongTopics { get; }
    IQueryable<Calendar> Calendars { get; }
    IQueryable<CalendarAttachState> CalendarAttachStates { get; }

    void Add(object entity);
    void Remove(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
