using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.Services.Song.Helpers;

public static class SongHelpers
{
    public static SongLinkType DeriveLinkKind(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure("url", "Song URL is required"),
            ]);

        var lower = url
            .Trim()
            .ToLowerInvariant();

        if (lower.Contains("youtube.com") || lower.Contains("youtu.be")) return SongLinkType.Youtube;

        if (lower.Contains("music.yandex") || lower.Contains("yandex.ru")) return SongLinkType.YandexMusic;

        if (lower.Contains("soundcloud.com")) return SongLinkType.Soundcloud;

        throw new ValidationException([
            new FluentValidation.Results.ValidationFailure("url", $"Unsupported song link URL: {url}"),
        ]);
    }

    public static List<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        return roles
            ?.Select(role => role.Trim())
            .Where(role => role.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToList() ?? [];
    }
}
