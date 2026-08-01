using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Application.Common.Models;
using CuMusicClub.Application.TodoLists;
using CuMusicClub.Application.TodoLists.Queries.GetTodos;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using CuMusicClub.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services;

public class TodoListService : ITodoListService
{
    private readonly IApplicationDbContext _context;

    public TodoListService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TodosVm> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var lists = await _context.TodoLists
            .Include(l => l.Items)
            .AsNoTracking()
            .OrderBy(t => t.Title)
            .ToListAsync(cancellationToken);

        return new TodosVm
        {
            PriorityLevels = Enum.GetValues<PriorityLevel>()
                .Select(p => new LookupDto { Id = (int)p, Title = p.ToString() })
                .ToList(),
            Colours =
            [
                new ColourDto { Code = Colour.Grey, Name = nameof(Colour.Grey) },
                new ColourDto { Code = Colour.Purple, Name = nameof(Colour.Purple) },
                new ColourDto { Code = Colour.Blue, Name = nameof(Colour.Blue) },
                new ColourDto { Code = Colour.Teal, Name = nameof(Colour.Teal) },
                new ColourDto { Code = Colour.Green, Name = nameof(Colour.Green) },
                new ColourDto { Code = Colour.Orange, Name = nameof(Colour.Orange) },
                new ColourDto { Code = Colour.Red, Name = nameof(Colour.Red) },
            ],
            Lists = lists.Select(l => new TodoListDto
            {
                Id = l.Id,
                Title = l.Title,
                Colour = l.Colour,
                Items = l.Items.Select(i => new TodoItemDto
                {
                    Id = i.Id,
                    ListId = i.ListId,
                    Title = i.Title,
                    Done = i.Done,
                    Priority = (int)i.Priority,
                    Note = i.Note
                }).ToList()
            }).ToList()
        };
    }

    public async Task<int> CreateAsync(string? title, string? colour, CancellationToken cancellationToken = default)
    {
        var entity = new TodoList { Title = title, Colour = colour is not null ? Colour.From(colour) : Colour.Grey };

        _context.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(int id, string? title, string? colour, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        Guard.Against.NotFound(id, entity);

        entity.Title = title;

        if (colour is not null)
        {
            entity.Colour = Colour.From(colour);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TodoLists
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        Guard.Against.NotFound(id, entity);
        _context.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
