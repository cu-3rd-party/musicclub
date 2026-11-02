namespace CuMusicClub.Domain.Entities;

/// <summary>
/// Сформированный отчёт о репетициях за период вместе с самим .xlsx-файлом.
/// Таблица <c>rehearsal_report</c>, на один период — один отчёт.
/// </summary>
public class RehearsalReport
{
    public Guid Id { get; set; }

    /// <summary>Первый день периода.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Последний день периода (включительно).</summary>
    public DateOnly PeriodEnd { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>Содержимое .xlsx.</summary>
    public byte[] Content { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public List<RehearsalReportDelivery> Deliveries { get; set; } = [];
}

/// <summary>
/// Факт отправки отчёта получателю. Таблица <c>rehearsal_report_delivery</c>.
/// </summary>
public class RehearsalReportDelivery
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public RehearsalReport Report { get; set; } = null!;

    /// <summary>Telegram username получателя в нижнем регистре, без @.</summary>
    public string Recipient { get; set; } = string.Empty;

    public long TgUserId { get; set; }

    public DateTimeOffset SentAt { get; set; }
}
