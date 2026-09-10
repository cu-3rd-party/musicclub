using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.Services.Telegram;

public interface ITelegramChatService
{
    /// <summary>
    /// Создать новый топик
    /// </summary>
    /// <param name="title">Название топика</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданный топик</returns>
    Task<SongTopic> CreateTopic(string title, Guid songId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить топик по ID
    /// </summary>
    /// <param name="topicId">ID топика</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Топик</returns>
    Task<SongTopic> GetTopic(long topicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удалить топик
    /// </summary>
    /// <param name="topicId">ID топика</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task DeleteTopic(long topicId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить сообщение в чат топика
    /// </summary>
    /// <param name="topicId">ID топика</param>
    /// <param name="message">Текст сообщения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task SendTopicMessage(long topicId, string message, CancellationToken cancellationToken = default);

    Task SendTopicPhoto(long topicId, string url, string? message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить сообщение в основной чат объявлений
    /// </summary>
    /// <param name="message">Текст сообщения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task SendGeneralMessage(string message, CancellationToken cancellationToken = default);

    /// <summary>Уведомление в чат роуди с inline-кнопкой «Взять группу».
    /// callback-данные кнопки: "roadie_accept:{ticketId}".</summary>
    Task SendRoadieTicketNotification(Guid ticketId,
        string songTitle,
        string artist,
        IEnumerable<SongRole> songRoles,
        RoadieTicketType ticketType = RoadieTicketType.Assignment,
        CancellationToken cancellationToken = default);

    /// <summary>Простое сообщение в чат роуди (без кнопки).</summary>
    Task SendRoadieMessage(string message, CancellationToken cancellationToken = default);

    /// <summary>Личное сообщение пользователю по его Telegram id.</summary>
    Task SendDirectMessage(long tgUserId, string message, CancellationToken cancellationToken = default);

    string BuildUserMention(ApplicationUser user);

    string BuildSongSystemLink(Domain.Entities.Song song);
}
