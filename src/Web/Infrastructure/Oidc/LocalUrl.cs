namespace CuMusicClub.Web.Infrastructure.Oidc;

public static class LocalUrl
{
    /// <summary>Только локальный путь (<c>/app/songs</c>), иначе <paramref name="fallback"/> — защита от open redirect.</summary>
    public static string OrDefault(string? url, string fallback = "/")
    {
        if (string.IsNullOrEmpty(url) || url[0] != '/')
            return fallback;
        if (url.Length > 1 && (url[1] == '/' || url[1] == '\\'))
            return fallback;
        if (url.Any(char.IsControl))
            return fallback;

        return url;
    }
}
