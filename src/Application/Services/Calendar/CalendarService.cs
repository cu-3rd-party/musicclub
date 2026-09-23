using System.Security.Cryptography;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Сервис для управления персональными календарями пользователей.
/// </summary>
public partial class CalendarService(
    ICalendarFeedRepository feedRepository,
    ICalendarEventRepository eventRepository,
    IApplicationUserRepository userRepository,
    IUnitOfWork unitOfWork) : ICalendarService
{
    public async Task<CalendarFeedDto> GetOrCreateFeedAsync(Guid userId, CancellationToken ct)
    {
        var existingFeed = await feedRepository.GetUserFeedAsync(userId, ct);
        
        if (existingFeed != null)
        {
            return MapToFeedDto(existingFeed);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        
        var newFeed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FeedToken = GenerateSecureToken(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createdFeed = await feedRepository.UpsertFeedAsync(newFeed, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapToFeedDto(createdFeed);
    }

    public async Task<CalendarFeedDto> RegenerateTokenAsync(Guid userId, CancellationToken ct)
    {
        var feed = await feedRepository.GetUserFeedAsync(userId, ct)
            ?? throw new NotFoundException($"Фид для пользователя {userId}", nameof(CalendarFeed));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        feed.FeedToken = GenerateSecureToken();
        feed.RevokedAt = null;
        feed.IsActive = true;

        var updatedFeed = await feedRepository.UpsertFeedAsync(feed, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapToFeedDto(updatedFeed);
    }

    public async Task<CalendarFeedDto> RevokeFeedAsync(Guid userId, CancellationToken ct)
    {
        var feed = await feedRepository.GetUserFeedAsync(userId, ct)
            ?? throw new NotFoundException($"Фид для пользователя {userId}", nameof(CalendarFeed));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        feed.IsActive = false;
        feed.RevokedAt = DateTime.UtcNow;

        var updatedFeed = await feedRepository.UpsertFeedAsync(feed, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapToFeedDto(updatedFeed);
    }

    public async Task<CalendarFeedDto?> GetFeedByTokenAsync(string token, CancellationToken ct)
    {
        var feed = await feedRepository.GetByTokenAsync(token, ct);
        
        if (feed == null || !feed.IsActive)
        {
            return null;
        }

        return MapToFeedDto(feed);
    }

    public async Task<List<CalendarEventDto>> GetEventsAsync(
        Guid userId, 
        DateTimeOffset? from, 
        DateTimeOffset? to, 
        CancellationToken ct)
    {
        var fromDate = from?.DateTime ?? DateTime.UtcNow.Date;
        var toDate = to?.DateTime ?? fromDate.AddMonths(1);

        var events = await eventRepository.GetUserActiveEventsAsync(userId, fromDate, toDate, ct);

        return events.Select(MapToEventDto).ToList();
    }

    public async Task<CalendarEventDto> CreateEventAsync(CreateCalendarEventRequest request, CancellationToken ct)
    {
        var user = await userRepository.FindByIdAsync(request.UserId, ct)
            ?? throw new NotFoundException($"Пользователь {request.UserId}", nameof(ApplicationUser));

        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt.DateTime,
            EndAt = request.EndAt.DateTime,
            Location = request.Location,
            EventType = request.EventType,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = null
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        
        await eventRepository.AddAsync(calendarEvent, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return MapToEventDto(calendarEvent);
    }

    public async Task DeleteEventAsync(Guid eventId, CancellationToken ct)
    {
        var calendarEvent = await eventRepository.FindByIdAsync(eventId, ct)
            ?? throw new NotFoundException(eventId.ToString(), nameof(CalendarEvent));

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        calendarEvent.DeletedAt = DateTime.UtcNow;
        calendarEvent.UpdatedAt = DateTime.UtcNow;

        eventRepository.Update(calendarEvent);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SyncEventFromTracklistAsync(Guid trackItemId, CancellationToken ct)
    {
        // TODO: Реализовать синхронизацию события из треклиста
        // Это требует доступа к сервису событий и треклистов
        await Task.CompletedTask;
    }

    private static CalendarFeedDto MapToFeedDto(CalendarFeed feed)
    {
        return new CalendarFeedDto
        {
            UserId = feed.UserId,
            FeedToken = feed.FeedToken,
            IcsUrl = string.Empty, // Будет установлен в Web layer
            IsActive = feed.IsActive,
            CreatedAt = feed.CreatedAt,
            RevokedAt = feed.RevokedAt
        };
    }

    private static CalendarEventDto MapToEventDto(CalendarEvent calendarEvent)
    {
        return new CalendarEventDto
        {
            Id = calendarEvent.Id,
            UserId = calendarEvent.UserId,
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            StartAt = calendarEvent.StartAt,
            EndAt = calendarEvent.EndAt,
            Location = calendarEvent.Location,
            EventType = calendarEvent.EventType,
            SourceType = calendarEvent.SourceType,
            SourceId = calendarEvent.SourceId
        };
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
