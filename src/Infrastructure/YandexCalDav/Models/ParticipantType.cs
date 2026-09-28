namespace CuMusicClub.Infrastructure.YandexCalDav.Models;

/// <summary>
///     Тип участника в CalDav
/// </summary>
/// <remarks>
///     Добавлен, чтобы однозначно определять тип CalDav-участника, не ориентируясь на Email.
///     В частности, позволяет разделять ведущего (хоста) и аудиторию:<br />
///     <c>ROLE=CHAIR;CUTYPE=INDIVIDUAL – ведущий (хост)</c><br />
///     <c>ROLE=CHAIR;CUTYPE=ROOM – аудитория</c>
/// </remarks>
public enum ParticipantType
{
    Unknown,
    Individual,
    Room
}
