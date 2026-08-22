namespace CuMusicClub.Application.Services.DataEntry;

public interface IDataEntryService
{
    /// <summary>
    /// Создает И СОХРАНЯЕТ В БД DataEntry
    /// </summary>
    /// <param name="content"></param>
    /// <param name="contentType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Domain.Entities.DataEntry> Create(byte[] content, string contentType, CancellationToken cancellationToken);
    // all other operations EF core can do easily by itself
    // Domain.Entities.Resource GetById(Guid resourceId);
    // void Delete(Guid resourceId);
}
