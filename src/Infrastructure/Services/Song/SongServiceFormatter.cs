using System.Net;
using System.Text;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Infrastructure.Services.Song;

public static partial class SongServiceFormatter
{
    private const int ForumTopicNameLimit = 100;

    public static string BuildSongTopicTitle(string title, string artist)
    {
        var name = BuildSongName(title, artist, "Песня");
        return TruncateRunes(name, ForumTopicNameLimit);
    }

    public static string BuildSongTopicMessage(
        string title,
        string artist,
        string? link,
        IReadOnlyList<RoleAssignmentDto> participants)
    {
        var main = BuildSongName(title, artist, "Песня");
        var mentions = BuildParticipantMentions(participants);

        if (mentions == "" && main == "" && string.IsNullOrWhiteSpace(link))
            return "";

        var b = new StringBuilder();
        b.Append("Тема для песни готова");
        AppendMessageBody(b, main, link, mentions, "Участники");

        return b.ToString();
    }

    public static string BuildSongCreatedMessage(string title, string artist, string? link, ApplicationUser? createdBy)
    {
        var b = new StringBuilder();
        b.Append("Добавлена новая песня: ");
        b.Append(BuildSongName(title, artist, null));
        b.AppendLine();
        b.AppendLine();
        if (createdBy != null)
        {
            b.Append($"Добавил(а): ");
            b.Append(BuildParticipantMention(createdBy));
            b.AppendLine();
        }
        b.Append($"<a href=\"{link}\">Послушать</a>");

        return b.ToString();
    }

    public static string BuildSongFullMessage(string title, string artist, string? link)
    {
        var main = BuildSongName(title, artist, null);

        var b = new StringBuilder();
        b.Append("Песня укомплектована");
        AppendMessageBody(b, main, link, null, null);

        return b.ToString();
    }

    public static string BuildParticipantMentions(IReadOnlyList<RoleAssignmentDto> participants)
    {
        if (participants.Count == 0)
            return "";

        var items = new List<string>(participants.Count);

        foreach (var p in participants)
        {
            items.Add(BuildParticipantMention(p.User));
        }

        return string.Join(", ", items);
    }

    public static string BuildParticipantMention(ApplicationUser user)
    {
        var userDto = new SongUserDto(user.Id, user.DisplayName, user.UserName, user.AvatarUrl, user.TgUserId);
        return BuildParticipantMention(userDto);
    }

    public static string BuildParticipantMention(SongUserDto user)
    {

        var escaped = WebUtility.HtmlEncode(user.DisplayName.Trim());

        // TgUserId может быть null, если пользователь не привязал Telegram
        if (user.TgUserId.HasValue)
            return $"<a href=\"tg://user?id={user.TgUserId}\">{escaped}</a>";

        return escaped;
    }

    private static string BuildSongName(string title, string artist, string? defaultValue)
    {
        title = EscapeAndTrim(title);
        artist = EscapeAndTrim(artist);

        return (title, artist) switch
        {
            (not "", not "") => $"{title} — {artist}",
            (not "", "") => title,
            ("", not "") => artist,
            _ => defaultValue ?? ""
        };
    }

    private static void AppendMessageBody(
        StringBuilder b,
        string main,
        string? link,
        string? mentions,
        string? participantsLabel)
    {
        if (main != "")
        {
            b.Append(": ");
            b.Append(main);
        }

        if (!string.IsNullOrEmpty(mentions))
        {
            b.AppendLine();
            b.AppendLine();
            b.Append(participantsLabel);
            b.Append(": ");
            b.Append(mentions);
        }

        if (!string.IsNullOrWhiteSpace(link))
        {
            b.AppendLine();
            b.AppendLine();
            b.Append(BuildSongLink(link));
        }
    }

    private static string BuildSongLink(string link)
    {
        // Если ссылка уже содержит http/https, используем как есть
        if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return link;
        }

        // Иначе добавляем https://
        return $"https://{link}";
    }

    private static string EscapeAndTrim(string? text)
    {
        return WebUtility.HtmlEncode(text?.Trim() ?? "");
    }

    private static string TruncateRunes(string text, int maxRunes)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var runeCount = text.EnumerateRunes().Count();
        if (runeCount <= maxRunes)
            return text;

        var runes = text.EnumerateRunes().Take(maxRunes);
        return string.Concat(runes.Select(r => r.ToString()));
    }
}
