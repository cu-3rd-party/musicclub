using System.Text.RegularExpressions;
using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.Services.Song.Helpers;

public static class SongHelpers
{
    private static readonly Regex YoutubeRegex = new(
        @"^(https?://)?(www\.|m\.)?(youtube\.com|youtu\.be)/.+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
    );

    private static readonly Regex YandexMusicRegex = new(
        @"^(https?://)?(music\.)?yandex\.(ru|com)/.+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
    );

    private static readonly Regex SoundcloudRegex = new(
        @"^(https?://)?(www\.)?soundcloud\.com/.+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
    );

    public static SongLinkType DeriveLinkKind(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure("url", "Song URL is required"),
            ]);

        var trimmed = url.Trim();

        if (YoutubeRegex.IsMatch(trimmed)) return SongLinkType.Youtube;

        if (YandexMusicRegex.IsMatch(trimmed)) return SongLinkType.YandexMusic;

        if (SoundcloudRegex.IsMatch(trimmed)) return SongLinkType.Soundcloud;

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
