using CuMusicClub.Domain.Common;

namespace CuMusicClub.Domain.Entities;

public class TodoList : IAuditableEntity
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public Colour Colour { get; set; } = Colour.Grey;
    public IList<TodoItem> Items { get; private set; } = new List<TodoItem>();

    public DateTimeOffset Created { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public string? LastModifiedBy { get; set; }
}
