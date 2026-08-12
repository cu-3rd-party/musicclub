using CuMusicClub.Domain.Common;

namespace CuMusicClub.Domain.Entities;

public class DataEntry : IAuditableEntity
{
    public Guid Id;

    /// <summary>
    /// MIME-тип
    /// </summary>
    public string ContentType = null!;

    /// <summary>
    /// Содержимое файла, храним в бд, s3 настраивать лень
    /// </summary>
    public byte[] Content = [];

    /// <summary>
    /// Используется для дедубликации
    /// </summary>
    public byte[] Hash = [];
    public long Size;

    public DateTimeOffset Created { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public Guid? LastModifiedBy { get; set; }
}
