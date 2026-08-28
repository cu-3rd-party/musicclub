using System.Net;
using System.Text;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Infrastructure.Services.Song;

public static partial class SongServiceFormatter
{
    /// <summary>
    /// Константа, которую задает тг - 128 символов. Но мы используем 100, а вообще желательно это понизить, но надо еще
    /// посмотреть как оно будет работать с песнями ибо бывают длинные названия
    /// </summary>
    private const int ForumTopicNameLimit = 100;

    public static string BuildSongTopicTitle(string title, string artist)
    {
        var name = BuildSongName(title, artist, "Песня");
        return TruncateRunes(name, ForumTopicNameLimit);
    }

    /// <summary>
    /// Собирает сообщение, которое должно быть отправлено в новосозданный когда песня заполнилась
    /// </summary>
    /// <param name="title"></param>
    /// <param name="artist"></param>
    /// <param name="link"></param>
    /// <param name="participants"></param>
    /// <returns></returns>
    public static string BuildSongFullTopicMessage(
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

    /// <summary>
    /// Собирает сообщение, которое должно быть отправлено в чат когда песню добавили в бота
    /// </summary>
    /// <param name="title"></param>
    /// <param name="artist"></param>
    /// <param name="link"></param>
    /// <param name="createdBy"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Собирает сообщение, которое должно быть отправлено в общий чат чтоб уведомить о заполненной песне
    /// </summary>
    /// <param name="title"></param>
    /// <param name="artist"></param>
    /// <param name="link"></param>
    /// <returns></returns>
    public static string BuildSongFullMessage(string title, string artist, string? link)
    {
        var main = BuildSongName(title, artist, null);

        var b = new StringBuilder();
        b.Append("Песня укомплектована");
        AppendMessageBody(b, main, link, null, null);

        return b.ToString();
    }

    /// <summary>
    /// Вызывает BuildParticipantMention на каждом пользователе из списка
    /// </summary>
    /// <param name="participants"></param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static string BuildParticipantMentions(IReadOnlyList<RoleAssignmentDto> participants, string separator = ", ")
    {
        if (participants.Count == 0)
            return "";

        var items = new List<string>(participants.Count);
        items.AddRange(participants.Select(p => BuildParticipantMention(p.User)));

        return string.Join(separator, items);
    }

    /// <summary>
    /// Оверлоад на BuildParticipantMention чтоб проще было
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static string BuildParticipantMention(ApplicationUser user)
    {
        var escaped = EscapeAndTrim(user.DisplayName.Trim());

        // TgUserId может быть null, если пользователь не привязал Telegram
        return user.TgUserId.HasValue ? $"<a href=\"tg://user?id={user.TgUserId}\">{escaped}</a>" : escaped;
    }

    /// <summary>
    /// Собираем (по возможности) корректное упоминание юзера через прямую ссылку на его аккаунт по тг айди
    /// Важно, то, что производит эта функция можно использовать только внутри тг. Браузер не сможет открыть ссылки вида
    /// tg://user?id=..., там можно использовать только ссылки по юзернейму вида https://web.telegram.org/k/#@igamamaev
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public static string BuildParticipantMention(SongUserDto user)
    {
        var appUser = new ApplicationUser
        {
            TgUserId = user.TgUserId,
            DisplayName = user.DisplayName,
            UserName = user.UserName,
            // TODO: заполнить все возможные поля
        };
        return BuildParticipantMention(appUser);
    }

    /// <summary>
    /// Собирает базовое комбинированное название песни
    /// </summary>
    /// <param name="title">Never gonna give you up</param>
    /// <param name="artist">Rick Astley</param>
    /// <param name="defaultValue">произошла ошибка</param>
    /// <returns>Never gonna give you up — Rick Astley</returns>
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

    /// <summary>
    /// : test — test
    ///
    /// Участники: <a href="tg://user?id=774301386">Игорь</a>
    ///
    /// <a href="https://www.youtube.com/watch?v=nRKJBpFFsuI&list=RDiqsnJJK8GA4&index=2">Послушать</a>
    /// </summary>
    /// <param name="b">куда собираем</param>
    /// <param name="main">BuildSongName(title, artist, "Песня")</param>
    /// <param name="link">ссылка на прослушивание песни</param>
    /// <param name="mentions">уже собранный через ... строка с упоминанием участников песни</param>
    /// <param name="participantsLabel">Участники</param>
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
            b.Append($"<a href=\"{EnsureProtocolPresent(link)}\">Послушать</a>");
        }
    }

    /// <summary>
    /// Удостоверяемся, что ссылка начинается с https:// или http://
    /// </summary>
    /// <param name="link">ссылка для которой это добавляем</param>
    /// <returns></returns>
    private static string EnsureProtocolPresent(string link)
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

    /// <summary>
    /// Очень аккуратно обрезаем строку в юникоде до нужной длины
    /// </summary>
    /// <param name="text">"Привет, мир!"</param>
    /// <param name="maxRunes">3</param>
    /// <returns>При</returns>
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
