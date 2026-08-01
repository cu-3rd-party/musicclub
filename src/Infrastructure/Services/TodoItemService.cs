using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Application.TodoItems;
using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services;

public class TodoItemService : ITodoItemService
{
    private readonly IApplicationDbContext _context;

    public TodoItemService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateAsync(string title, int listId, CancellationToken cancellationToken = default)
    {
        var entity = new TodoItem { ListId = listId, Title = title, Done = false };

        _context.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        Guard.Against.NotFound(id, entity);
        _context.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(int id, string? title, bool done, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        Guard.Against.NotFound(id, entity);

        entity.Title = title;
        entity.Done = done;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDetailAsync(int id, int listId, CuMusicClub.Domain.Enums.PriorityLevel priority,
        string? note, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        Guard.Against.NotFound(id, entity);

        entity.ListId = listId;
        entity.Priority = priority;
        entity.Note = note;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
