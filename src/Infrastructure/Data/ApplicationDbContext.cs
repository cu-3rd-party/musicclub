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

    public DbSet<Calendar> Calendars => Set<Calendar>();
    public DbSet<CalendarAttachState> CalendarAttachStates => Set<CalendarAttachState>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<SongRole> SongRoles => Set<SongRole>();
    public DbSet<SongRoleAssignment> SongRoleAssignments => Set<SongRoleAssignment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<TgAuthLink> TgAuthLinks => Set<TgAuthLink>();

    IQueryable<ApplicationUser> IApplicationDbContext.Users => Users;
    IQueryable<Calendar> IApplicationDbContext.Calendars => Calendars;
    IQueryable<CalendarAttachState> IApplicationDbContext.CalendarAttachStates => CalendarAttachStates;
    IQueryable<Song> IApplicationDbContext.Songs => Songs;
    IQueryable<SongRole> IApplicationDbContext.SongRoles => SongRoles;
    IQueryable<SongRoleAssignment> IApplicationDbContext.SongRoleAssignments => SongRoleAssignments;
    IQueryable<UserSession> IApplicationDbContext.UserSessions => UserSessions;
    IQueryable<TgAuthLink> IApplicationDbContext.TgAuthLinks => TgAuthLinks;

    void IApplicationDbContext.Add(object entity) => Add(entity);
    void IApplicationDbContext.Remove(object entity) => Remove(entity);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresEnum<CuMusicClub.Domain.Enums.SongLinkType>();
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
