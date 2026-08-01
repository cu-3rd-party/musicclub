using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CuMusicClub.Infrastructure.Data.Interceptors;

public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        StoreDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        StoreDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void StoreDomainEvents(DbContext? context)
    {
        if (context == null) return;

        var entities = context.ChangeTracker
            .Entries<TodoItem>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            var events = entity.DomainEvents.ToList();
            entity.ClearDomainEvents();

            foreach (var domainEvent in events)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = domainEvent.GetType().Name,
                    EventData = System.Text.Json.JsonSerializer.Serialize(domainEvent),
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = null
                });
            }
        }
    }
}
