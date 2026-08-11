using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Domain.ValueObjects;

public static class SongThumbnail
{
    public static string? Normalize(string? customUrl, SongLinkType linkKind, string? linkUrl)
    {
        if (!string.IsNullOrWhiteSpace(customUrl)) return customUrl;

        return linkKind switch
        {
            SongLinkType.Youtube => ExtractYoutubeThumbnail(linkUrl),
            _ => null,
        };
    }

    private static string? ExtractYoutubeThumbnail(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var videoId = ParseYouTubeVideoId(url);
        return videoId is not null ? $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg" : null;
    }

    private static string? ParseYouTubeVideoId(string url)
    {
        var uri = new Uri(url);
        if (uri.Host.Contains("youtu.be")) return uri.AbsolutePath.Trim('/');

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["v"];
    }
}
