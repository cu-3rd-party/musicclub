using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Domain.Abstractions;

public interface IRehearsalReportRepository : IRepository<RehearsalReport>
{
    /// <summary>Отчёт за период вместе с файлом и доставками.</summary>
    Task<RehearsalReport?> FindByPeriodStartAsync(DateOnly periodStart, CancellationToken ct = default);

    /// <summary>Кому что уже отправлено: начало периода → получатели. Файлы не загружаются.</summary>
    Task<IReadOnlyDictionary<DateOnly, IReadOnlySet<string>>> GetDeliveredRecipientsAsync(
        CancellationToken ct = default);

    /// <summary>Записывает факт отправки отчёта получателю.</summary>
    Task AddDeliveryAsync(RehearsalReportDelivery delivery, CancellationToken ct = default);
}
