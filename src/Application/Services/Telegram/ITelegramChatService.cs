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
}
