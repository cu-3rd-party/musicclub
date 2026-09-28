namespace CuMusicClub.Infrastructure.YandexCalDav.Config;

public record YandexCaldavConfig
{
    public string BaseUrl { get; set; } = "https://caldav.yandex.ru";
    public string User { get; set; } = string.Empty;
    public string OrganizerCN { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
