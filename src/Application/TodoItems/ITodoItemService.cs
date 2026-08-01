namespace CuMusicClub.Application.TodoItems;

public interface ITodoItemService
{
    Task<int> CreateAsync(string title, int listId, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string? title, bool done, CancellationToken cancellationToken = default);

    Task UpdateDetailAsync(int id, int listId, Domain.Enums.PriorityLevel priority, string? note,
        CancellationToken cancellationToken = default);
}
