namespace CuMusicClub.Domain.Entities;

public class TgAuthLink
{
    public Guid Id { get; set; }

    public long? TgUserId { get; set; }
    // TODO: стоит добавить сюда дату создания и удалять ссылки старше суток
}
