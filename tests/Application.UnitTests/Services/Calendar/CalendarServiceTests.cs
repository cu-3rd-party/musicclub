using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Moq;
using NUnit.Framework;
using Shouldly;
using NotFoundException = Ardalis.GuardClauses.NotFoundException;

namespace CuMusicClub.Application.UnitTests.Services.Calendar;

[TestFixture]
[TestOf(typeof(CalendarService))]
public class CalendarServiceTests
{
    private Mock<ICalendarFeedRepository> _feedRepository = null!;
    private Mock<ICalendarEventRepository> _eventRepository = null!;
    private Mock<IApplicationUserRepository> _userRepository = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private CalendarService _service = null!;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTime _nowUtc = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _feedRepository = new Mock<ICalendarFeedRepository>();
        _eventRepository = new Mock<ICalendarEventRepository>();
        _userRepository = new Mock<IApplicationUserRepository>();
        _unitOfWork = new Mock<IUnitOfWork>();

        _service = new CalendarService(
            _feedRepository.Object,
            _eventRepository.Object,
            _userRepository.Object,
            _unitOfWork.Object);
    }

    #region GetOrCreateFeedAsync

    [Test]
    public async Task GetOrCreateFeedAsync_WhenFeedExists_ReturnsExistingFeed()
    {
        // Arrange
        var existingFeed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FeedToken = "existing-token",
            IsActive = true,
            CreatedAt = _nowUtc
        };

        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFeed);

        // Act
        var result = await _service.GetOrCreateFeedAsync(_userId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBe(_userId);
        result.FeedToken.ShouldBe("existing-token");
        result.IsActive.ShouldBeTrue();
        
        _feedRepository.Verify(r => r.UpsertFeedAsync(It.IsAny<CalendarFeed>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetOrCreateFeedAsync_WhenFeedNotExists_CreatesNewFeed()
    {
        // Arrange
        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarFeed?)null);

        CalendarFeed? capturedFeed = null;
        _feedRepository
            .Setup(r => r.UpsertFeedAsync(It.IsAny<CalendarFeed>(), It.IsAny<CancellationToken>()))
            .Callback<CalendarFeed, CancellationToken>((f, _) => capturedFeed = f)
            .ReturnsAsync((CalendarFeed f, CancellationToken _) => f);

        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ITransaction>());

        // Act
        var result = await _service.GetOrCreateFeedAsync(_userId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.UserId.ShouldBe(_userId);
        result.FeedToken.ShouldNotBeNullOrEmpty();
        result.IsActive.ShouldBeTrue();

        capturedFeed.ShouldNotBeNull();
        capturedFeed.UserId.ShouldBe(_userId);
        capturedFeed.IsActive.ShouldBeTrue();
        capturedFeed.FeedToken.ShouldBe(result.FeedToken);
    }

    #endregion

    #region RegenerateTokenAsync

    [Test]
    public async Task RegenerateTokenAsync_WhenFeedExists_GeneratesNewToken()
    {
        // Arrange
        var oldToken = "old-token-123";
        var existingFeed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FeedToken = oldToken,
            IsActive = true,
            CreatedAt = _nowUtc
        };

        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFeed);

        CalendarFeed? capturedFeed = null;
        _feedRepository
            .Setup(r => r.UpsertFeedAsync(It.IsAny<CalendarFeed>(), It.IsAny<CancellationToken>()))
            .Callback<CalendarFeed, CancellationToken>((f, _) => capturedFeed = f)
            .ReturnsAsync((CalendarFeed f, CancellationToken _) => f);

        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ITransaction>());

        // Act
        var result = await _service.RegenerateTokenAsync(_userId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.FeedToken.ShouldNotBe(oldToken);
        result.IsActive.ShouldBeTrue();

        capturedFeed.ShouldNotBeNull();
        capturedFeed.FeedToken.ShouldBe(result.FeedToken);
        capturedFeed.RevokedAt.ShouldBeNull();
    }

    [Test]
    public async Task RegenerateTokenAsync_WhenFeedNotExists_ThrowsNotFoundException()
    {
        // Arrange
        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarFeed?)null);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(async () => await _service.RegenerateTokenAsync(_userId, CancellationToken.None));
    }

    #endregion

    #region RevokeFeedAsync

    [Test]
    public async Task RevokeFeedAsync_WhenFeedExists_SetsInactiveAndRevokedAt()
    {
        // Arrange
        var existingFeed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FeedToken = "active-token",
            IsActive = true,
            CreatedAt = _nowUtc
        };

        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFeed);

        CalendarFeed? capturedFeed = null;
        _feedRepository
            .Setup(r => r.UpsertFeedAsync(It.IsAny<CalendarFeed>(), It.IsAny<CancellationToken>()))
            .Callback<CalendarFeed, CancellationToken>((f, _) => capturedFeed = f)
            .ReturnsAsync((CalendarFeed f, CancellationToken _) => f);

        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ITransaction>());

        // Act
        var result = await _service.RevokeFeedAsync(_userId, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.IsActive.ShouldBeFalse();
        result.RevokedAt.ShouldNotBeNull();

        capturedFeed.ShouldNotBeNull();
        capturedFeed.IsActive.ShouldBeFalse();
        capturedFeed.RevokedAt.ShouldNotBeNull();
    }

    [Test]
    public async Task RevokeFeedAsync_WhenFeedNotExists_ThrowsNotFoundException()
    {
        // Arrange
        _feedRepository
            .Setup(r => r.GetUserFeedAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarFeed?)null);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(async () => await _service.RevokeFeedAsync(_userId, CancellationToken.None));
    }

    #endregion

    #region GetFeedByTokenAsync

    [Test]
    public async Task GetFeedByTokenAsync_WhenTokenValidAndActive_ReturnsFeed()
    {
        // Arrange
        var token = "valid-token";
        var feed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FeedToken = token,
            IsActive = true,
            CreatedAt = _nowUtc
        };

        _feedRepository
            .Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(feed);

        // Act
        var result = await _service.GetFeedByTokenAsync(token, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.FeedToken.ShouldBe(token);
        result.IsActive.ShouldBeTrue();
    }

    [Test]
    public async Task GetFeedByTokenAsync_WhenTokenInvalid_ReturnsNull()
    {
        // Arrange
        _feedRepository
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarFeed?)null);

        // Act
        var result = await _service.GetFeedByTokenAsync("invalid-token", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task GetFeedByTokenAsync_WhenFeedInactive_ReturnsNull()
    {
        // Arrange
        var token = "revoked-token";
        var feed = new CalendarFeed
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            FeedToken = token,
            IsActive = false,
            CreatedAt = _nowUtc,
            RevokedAt = _nowUtc
        };

        _feedRepository
            .Setup(r => r.GetByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(feed);

        // Act
        var result = await _service.GetFeedByTokenAsync(token, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region GetEventsAsync

    [Test]
    public async Task GetEventsAsync_WithDateRange_ReturnsFilteredEvents()
    {
        // Arrange
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);

        var events = new List<CalendarEvent>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Event 1",
                StartAt = new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc),
                EventType = Domain.Enums.CalendarEventType.Rehearsal,
                SourceType = "manual",
                DeletedAt = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Event 2",
                StartAt = new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 9, 20, 16, 0, 0, DateTimeKind.Utc),
                EventType = Domain.Enums.CalendarEventType.Performance,
                SourceType = "tracklist",
                DeletedAt = null
            }
        };

        _eventRepository
            .Setup(r => r.GetUserActiveEventsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _service.GetEventsAsync(_userId, from, to, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Title.ShouldBe("Event 1");
        result[1].Title.ShouldBe("Event 2");
    }

    [Test]
    public async Task GetEventsAsync_WithoutDateRange_UsesDefaultRange()
    {
        // Arrange
        var events = new List<CalendarEvent>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                Title = "Default Range Event",
                StartAt = DateTime.UtcNow.AddDays(1),
                EndAt = DateTime.UtcNow.AddDays(2),
                EventType = Domain.Enums.CalendarEventType.Personal,
                SourceType = "manual",
                DeletedAt = null
            }
        };

        _eventRepository
            .Setup(r => r.GetUserActiveEventsAsync(_userId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _service.GetEventsAsync(_userId, null, null, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
    }

    #endregion
}
