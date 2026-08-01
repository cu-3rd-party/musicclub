using System.Reflection;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<SongRole> SongRoles => Set<SongRole>();
    public DbSet<SongRoleAssignment> SongRoleAssignments => Set<SongRoleAssignment>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventTrackItem> EventTrackItems => Set<EventTrackItem>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<TgAuthUser> TgAuthUsers => Set<TgAuthUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SongTopic> SongTopics => Set<SongTopic>();
    public DbSet<Calendar> Calendars => Set<Calendar>();
    public DbSet<CalendarAttachState> CalendarAttachStates => Set<CalendarAttachState>();

    IQueryable<AppUser> IApplicationDbContext.AppUsers => AppUsers;
    IQueryable<UserPermission> IApplicationDbContext.UserPermissions => UserPermissions;
    IQueryable<Song> IApplicationDbContext.Songs => Songs;
    IQueryable<SongRole> IApplicationDbContext.SongRoles => SongRoles;
    IQueryable<SongRoleAssignment> IApplicationDbContext.SongRoleAssignments => SongRoleAssignments;
    IQueryable<Event> IApplicationDbContext.Events => Events;
    IQueryable<EventTrackItem> IApplicationDbContext.EventTrackItems => EventTrackItems;
    IQueryable<EventParticipant> IApplicationDbContext.EventParticipants => EventParticipants;
    IQueryable<TgAuthUser> IApplicationDbContext.TgAuthUsers => TgAuthUsers;
    IQueryable<RefreshToken> IApplicationDbContext.RefreshTokens => RefreshTokens;
    IQueryable<SongTopic> IApplicationDbContext.SongTopics => SongTopics;
    IQueryable<Calendar> IApplicationDbContext.Calendars => Calendars;
    IQueryable<CalendarAttachState> IApplicationDbContext.CalendarAttachStates => CalendarAttachStates;

    void IApplicationDbContext.Add(object entity) => Add(entity);
    void IApplicationDbContext.Remove(object entity) => Remove(entity);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
