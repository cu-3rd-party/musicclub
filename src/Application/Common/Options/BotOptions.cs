namespace CuMusicClub.Application.Common.Options;

public class BotOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string WebAppUrl { get; set; } = "https://localhost:7273";

    /// <summary>
    /// Публичный URL вебхука (https://host/api/telegram/webhook). Пусто — бот работает через long polling.
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// secret_token вебхука (заголовок X-Telegram-Bot-Api-Secret-Token). Пусто — выводится из BotToken.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
