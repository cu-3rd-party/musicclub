using System.Text.RegularExpressions;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Infrastructure.Services;

public static partial class SongThumbnail
{
    public static string? Normalize(string? customUrl, SongLinkType linkKind, string? linkUrl)
    {
        customUrl = customUrl?.Trim();
        if (!string.IsNullOrEmpty(customUrl))
        {
            return customUrl;
        }

        return linkKind switch
        {
            SongLinkType.Youtube => ExtractYouTubeThumbnail(linkUrl),
            _ => null,
        };
    }

    private static string? ExtractYouTubeThumbnail(string? url)
    {
        var videoId = ExtractYouTubeVideoId(url);
        return string.IsNullOrEmpty(videoId) ? null : $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
    }

    private static string? ExtractYouTubeVideoId(string? url)
    {
        if (url is null)
        {
            return null;
        }

        var match = WatchPattern().Match(url);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = EmbedPattern().Match(url);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = ShortPathPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(?:youtube\.com/watch\?v=|youtu\.be/)([a-zA-Z0-9_-]{11})")]
    private static partial Regex WatchPattern();

    [GeneratedRegex(@"youtube\.com/embed/([a-zA-Z0-9_-]{11})")]
    private static partial Regex EmbedPattern();

    [GeneratedRegex(@"youtube\.com/v/([a-zA-Z0-9_-]{11})")]
    private static partial Regex ShortPathPattern();
}
