using System.Reflection;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> Users
    {
        get { return Set<ApplicationUser>(); }
    }

    public DbSet<UserPermission> UserPermissions
    {
        get { return Set<UserPermission>(); }
    }

    public DbSet<Calendar> Calendars
    {
        get { return Set<Calendar>(); }
    }

    public DbSet<CalendarAttachState> CalendarAttachStates
    {
        get { return Set<CalendarAttachState>(); }
    }

    public DbSet<Song> Songs
    {
        get { return Set<Song>(); }
    }

    public DbSet<SongRole> SongRoles
    {
        get { return Set<SongRole>(); }
    }

    public DbSet<SongRoleAssignment> SongRoleAssignments
    {
        get { return Set<SongRoleAssignment>(); }
    }

    public DbSet<UserSession> UserSessions
    {
        get { return Set<UserSession>(); }
    }

    public DbSet<RefreshToken> RefreshTokens
    {
        get { return Set<RefreshToken>(); }
    }

    public DbSet<TgAuthLink> TgAuthLinks
    {
        get { return Set<TgAuthLink>(); }
    }

    public DbSet<DataEntry> DataEntries
    {
        get { return Set<DataEntry>(); }
    }

    public DbSet<SongTopic> SongTopics
    {
        get { return Set<SongTopic>(); }
    }

    public DbSet<RoleTitle> RoleTitles
    {
        get { return Set<RoleTitle>(); }
    }

    public DbSet<UserPreferences> UserPreferencesEnumerable
    {
        get { return Set<UserPreferences>(); }
    }

    public DbSet<RehearsalBooking> RehearsalBookings
    {
        get { return Set<RehearsalBooking>(); }
    }

    public DbSet<CalendarEvent> CalendarEvents
    {
        get { return Set<CalendarEvent>(); }
    }

    public DbSet<CalendarFeed> CalendarFeeds
    {
        get { return Set<CalendarFeed>(); }
    }

    public DbSet<Event> Events
    {
        get { return Set<Event>(); }
    }

    public DbSet<EventTrackItem> EventTrackItems
    {
        get { return Set<EventTrackItem>(); }
    }

    public DbSet<EventParticipant> EventParticipants
    {
        get { return Set<EventParticipant>(); }
    }

    public DbSet<SongRoadie> SongRoadies
    {
        get { return Set<SongRoadie>(); }
    }

    public DbSet<RoadieTicket> RoadieTickets
    {
        get { return Set<RoadieTicket>(); }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresEnum<Domain.Enums.SongLinkType>();
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}