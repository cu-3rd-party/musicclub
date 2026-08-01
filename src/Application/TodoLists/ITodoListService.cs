using CuMusicClub.Application.TodoLists.Queries.GetTodos;

namespace CuMusicClub.Application.TodoLists;

public interface ITodoListService
{
    Task<TodosVm> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(string? title, string? colour, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string? title, string? colour, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
