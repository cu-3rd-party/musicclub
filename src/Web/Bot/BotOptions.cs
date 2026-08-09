namespace CuMusicClub.Web.Bot;

public class BotOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string WebAppUrl { get; set; } = "https://localhost:7273";
}
