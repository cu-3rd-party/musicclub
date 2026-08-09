using CuMusicClub.Domain.Common;

namespace CuMusicClub.Domain.Entities;

public class TgAuthLink : IAuditableEntity
{
    public Guid Id { get; set; }
    public long? TgUserId { get; set; }
    
    public DateTimeOffset Created { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
