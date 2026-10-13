using Prometheus;

namespace CuMusicClub.Web.Bot;

/// <summary>
/// Prometheus-метрики Telegram-бота (отдаются на /metrics отдельного порта, см. Metrics:Port).
/// </summary>
public static class BotMetrics
{
    /// <summary>
    /// Команды, которые бот обрабатывает; остальные считаются как «unknown», чтобы не раздувать кардинальность.
    /// </summary>
    private static readonly HashSet<string> KnownCommands =
    [
        "start", "help", "info", "roadie", "ticket_roadie", "call_my_roadie", "ping",
        "check", "slots", "slots_with", "take", "take_with", "approve", "reject", "cancel",
        "status", "update", "day"
    ];

    private static readonly Counter Updates = Metrics.CreateCounter(
        "musicclub_bot_updates_total", "Обработанные апдейты Telegram по типу и результату.",
        "type", "result");

    private static readonly Histogram UpdateDuration = Metrics.CreateHistogram(
        "musicclub_bot_update_duration_seconds", "Время обработки апдейта Telegram.",
        new HistogramConfiguration
        {
            LabelNames = ["type"],
            Buckets = Histogram.ExponentialBuckets(0.01, 2, 12)
        });

    private static readonly Counter Commands = Metrics.CreateCounter(
        "musicclub_bot_commands_total", "Полученные команды бота.", "command");

    private static readonly Counter Cooldowns = Metrics.CreateCounter(
        "musicclub_bot_command_cooldowns_total", "Команды, отклонённые из-за cooldown.", "command");

    private static readonly Counter PollingErrors = Metrics.CreateCounter(
        "musicclub_bot_polling_errors_total", "Ошибки Telegram-клиента (polling и обработчики).", "source");

    private static readonly Counter WebhookRequests = Metrics.CreateCounter(
        "musicclub_bot_webhook_requests_total", "Запросы к вебхуку Telegram по результату.", "result");

    private static readonly Gauge WebhookQueue = Metrics.CreateGauge(
        "musicclub_bot_webhook_queue_length", "Апдейты вебхука, ожидающие обработки.");

    public static Prometheus.ITimer MeasureUpdate(string type) => UpdateDuration.WithLabels(type).NewTimer();

    public static void UpdateHandled(string type, bool success) =>
        Updates.WithLabels(type, success ? "ok" : "error").Inc();

    public static void CommandReceived(string command) => Commands.WithLabels(Normalize(command)).Inc();

    public static void CommandCooldown(string command) => Cooldowns.WithLabels(Normalize(command)).Inc();

    public static void PollingError(string source) => PollingErrors.WithLabels(source).Inc();

    public static void WebhookRequest(string result) => WebhookRequests.WithLabels(result).Inc();

    /// <summary>Глубина очереди апдейтов вебхука снимается при каждом scrape.</summary>
    public static void TrackQueue(TelegramUpdateQueue queue) =>
        Metrics.DefaultRegistry.AddBeforeCollectCallback(() => WebhookQueue.Set(queue.Count));

    private static string Normalize(string command) => KnownCommands.Contains(command) ? command : "unknown";
}
