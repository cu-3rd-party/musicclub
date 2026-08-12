namespace CuMusicClub.Application.Services.Resource;

public interface IDataEntryService
{
    Domain.Entities.DataEntry Create(byte[] content, string contentType);
    // all other operations EF core can do easily by itself
    // Domain.Entities.Resource GetById(Guid resourceId);
    // void Delete(Guid resourceId);
}
