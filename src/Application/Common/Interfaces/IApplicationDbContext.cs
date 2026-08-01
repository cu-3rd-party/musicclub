using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    IQueryable<TodoList> TodoLists { get; }
    IQueryable<TodoItem> TodoItems { get; }
    IQueryable<ShortenedUrl> ShortenedUrls { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }
    IQueryable<Bookmark> Bookmarks { get; }

    void Add(object entity);
    void Remove(object entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
