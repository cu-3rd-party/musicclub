namespace CuMusicClub.Application.Services.Calendar;

/// <summary>
/// Включена ли запись бронирований во внешний календарь (Яндекс).
/// Если выключена — бронирования живут только в боте, а backfill догонит их, когда интеграцию включат.
/// </summary>
public interface ICalendarIntegration
{
    bool IsSyncEnabled { get; }
}
