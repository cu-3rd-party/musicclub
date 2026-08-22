using System.Security.Cryptography;
using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.Services.DataEntry;

public class DataEntryService(ApplicationDbContext db) : IDataEntryService
{
    public async Task<Domain.Entities.DataEntry> Create(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (content.Length == 0) throw new InvalidOperationException("Data Entry content cannot be empty");

        var hash = SHA256.HashData(content);

        var existing = await db.DataEntries.FirstOrDefaultAsync(x => x.Hash == hash, cancellationToken);
        if (existing is not null) return existing;

        var entry = new Domain.Entities.DataEntry
        {
            Id = Guid.NewGuid(),
            Content = content,
            Hash = hash,
            ContentType = contentType,
            Size = content.LongLength,
        };

        db.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return entry;
    }
}
