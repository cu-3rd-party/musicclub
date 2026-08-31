namespace CuMusicClub.Infrastructure.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    /// <summary>
    /// после этого будет добавляться /data/{id}
    /// </summary>
    public string ImageBaseUrl { get; set; } = "https://dev.musicclub.cu3rd.ru/api/v1";
    public bool SkipChatMembershipCheck { get; set; }
}
