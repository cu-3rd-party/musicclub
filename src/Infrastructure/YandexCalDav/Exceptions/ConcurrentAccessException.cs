namespace CuMusicClub.Infrastructure.YandexCalDav.Exceptions;

public class ConcurrentAccessException(string message) : Exception(message)
{
}
