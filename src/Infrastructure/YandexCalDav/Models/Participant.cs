namespace CuMusicClub.Infrastructure.YandexCalDav.Models;

public sealed class Participant(
    string email,
    string? name = null,
    ParticipantRole role = ParticipantRole.Required,
    ParticipantStatus status = ParticipantStatus.NeedsAction,
    ParticipantType type = ParticipantType.Individual)
    : IEquatable<Participant>
{
    public string Email { get; } = email;
    public string Name { get; } = !string.IsNullOrEmpty(name) ? name : email;
    public ParticipantRole Role { get; } = role;
    public ParticipantStatus Status { get; } = status;
    public ParticipantType Type { get; } = type;

    public bool IsLocation
    {
        get
        {
            return Role == ParticipantRole.Chair &&
                   Type == ParticipantType.Room;
        }
    }

    public bool Equals(Participant? other)
    {
        if (other is null) return false;

        if (ReferenceEquals(this, other)) return true;

        return Email == other.Email;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;

        if (ReferenceEquals(this, obj)) return true;

        if (obj.GetType() != GetType()) return false;

        return Equals((Participant) obj);
    }

    public override int GetHashCode()
    {
        return Email.GetHashCode();
    }

    /// <summary>
    ///     Создать обязательного участника события
    /// </summary>
    public static Participant CreateRequired(string email, string? name = null)
    {
        return new Participant(
            email, name, ParticipantRole.Required);
    }

    /// <summary>
    ///     Создать опционального участника события
    /// </summary>
    public static Participant CreateOptional(string email, string? name = null)
    {
        return new Participant(
            email, name, ParticipantRole.Optional);
    }

    /// <summary>
    ///     Создать участника события, как ведущего (хоста)
    /// </summary>
    /// <remarks>Не путать с Organizer, которым является системный пользователь модуля Timetables</remarks>
    public static Participant CreateHost(string email)
    {
        return new Participant(
            email, role: ParticipantRole.Chair);
    }

    /// <summary>
    ///     Создать участника события, представляющего собой аудиторию
    /// </summary>
    public static Participant CreateLocation(string email, string name)
    {
        return new Participant(
            email,
            name,
            ParticipantRole.Chair,
            type: ParticipantType.Room);
    }
}
