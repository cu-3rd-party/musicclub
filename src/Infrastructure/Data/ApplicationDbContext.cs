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

    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<ShortenedUrl> ShortenedUrls => Set<ShortenedUrl>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    IQueryable<TodoList> IApplicationDbContext.TodoLists => TodoLists;
    IQueryable<TodoItem> IApplicationDbContext.TodoItems => TodoItems;
    IQueryable<ShortenedUrl> IApplicationDbContext.ShortenedUrls => ShortenedUrls;
    IQueryable<OutboxMessage> IApplicationDbContext.OutboxMessages => OutboxMessages;
    IQueryable<Bookmark> IApplicationDbContext.Bookmarks => Bookmarks;

    void IApplicationDbContext.Add(object entity) => Add(entity);
    void IApplicationDbContext.Remove(object entity) => Remove(entity);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
