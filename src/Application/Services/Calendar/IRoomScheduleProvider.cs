namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Расписание репетиционного зала (календарь базы, который ведут вне бота).
/// </summary>
public interface IRoomScheduleProvider
{
    /// <summary>
    /// Задан ли источник расписания зала.
    /// </summary>
    bool IsConfigured { get; }

    Task<IReadOnlyList<ExternalScheduleEvent>> GetRoomEventsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
