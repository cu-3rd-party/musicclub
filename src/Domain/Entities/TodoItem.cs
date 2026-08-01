using CuMusicClub.Domain.Common;

namespace CuMusicClub.Domain.Entities;

public class TodoItem : IAuditableEntity
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public string? Title { get; set; }
    public string? Note { get; set; }
    public PriorityLevel Priority { get; set; }

    private bool _done;

    public bool Done
    {
        get => _done;
        set
        {
            if (value && !_done)
            {
                _domainEvents.Add(new TodoItemCompletedEvent(Id));
            }

            _done = value;
        }
    }

    public TodoList List { get; set; } = null!;

    public DateTimeOffset Created { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public string? LastModifiedBy { get; set; }

    private readonly List<object> _domainEvents = [];
    public IReadOnlyCollection<object> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();
}
