using System.Security.Cryptography;

namespace CuMusicClub.Application.Services.Resource;

public class DataEntryService : IDataEntryService
{
    public Domain.Entities.DataEntry Create(byte[] content, string contentType)
    {
        return new Domain.Entities.DataEntry
        {
            Id = Guid.NewGuid(),
            Content = content,
            Hash = SHA256.HashData(content),
            ContentType = contentType,
            Size = content.LongLength,
        };
    }
}
