namespace CuMusicClub.Application.Common.Options;

public class RehearsalReportOptions
{
    public const string SectionName = "RehearsalReports";

    /// <summary>Формировать и рассылать отчёты. Включается только на проде.</summary>
    public bool Enabled { get; set; }

    /// <summary>Telegram username получателей, без @.</summary>
    public string[] Recipients { get; set; } = [];

    /// <summary>Первый месяц, за который нужен отчёт; более ранние не формируются.</summary>
    public DateOnly FirstPeriod { get; set; } = new(2026, 10, 1);
}
