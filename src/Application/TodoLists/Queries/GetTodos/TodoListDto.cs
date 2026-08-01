namespace CuMusicClub.Application.TodoLists.Queries.GetTodos;

public class TodoListDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public string? Colour { get; init; }
    public IReadOnlyCollection<TodoItemDto> Items { get; init; } = [];
}
