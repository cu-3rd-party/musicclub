using System.Reflection;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
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

    IQueryable<ApplicationUser> IApplicationDbContext.Users
    {
        get { return Users; }
    }

    IQueryable<Calendar> IApplicationDbContext.Calendars
    {
        get { return Calendars; }
    }

    IQueryable<CalendarAttachState> IApplicationDbContext.CalendarAttachStates
    {
        get { return CalendarAttachStates; }
    }

    IQueryable<Song> IApplicationDbContext.Songs
    {
        get { return Songs; }
    }

    IQueryable<SongRole> IApplicationDbContext.SongRoles
    {
        get { return SongRoles; }
    }

    IQueryable<SongRoleAssignment> IApplicationDbContext.SongRoleAssignments
    {
        get { return SongRoleAssignments; }
    }

    IQueryable<UserSession> IApplicationDbContext.UserSessions
    {
        get { return UserSessions; }
    }

    IQueryable<TgAuthLink> IApplicationDbContext.TgAuthLinks
    {
        get { return TgAuthLinks; }
    }

    IQueryable<DataEntry> IApplicationDbContext.DataEntries
    {
        get { return DataEntries; }
    }

    IQueryable<SongTopic> IApplicationDbContext.SongTopics
    {
        get { return SongTopics; }
    }

    void IApplicationDbContext.Add(object entity)
    {
        Add(entity);
    }

    void IApplicationDbContext.Remove(object entity)
    {
        Remove(entity);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresEnum<Domain.Enums.SongLinkType>();
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
