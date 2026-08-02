namespace CuMusicClub.Web.Bot;

public class BotOptions
{
    public const string SectionName = "Bot";

    public string BotToken { get; set; } = string.Empty;
    public string DefaultWebAppUrl { get; set; } = "http://localhost:5173";
    public string EmailDomain { get; set; } = "edu.centraluniversity.ru";
}
