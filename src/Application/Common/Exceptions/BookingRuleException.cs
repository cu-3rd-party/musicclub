namespace CuMusicClub.Application.Common.Exceptions;

/// <summary>
/// Нарушено правило бронирования. Сообщение предназначено для пользователя.
/// </summary>
public class BookingRuleException : Exception
{
    public BookingRuleException(string message) : base(message)
    {
    }
}
