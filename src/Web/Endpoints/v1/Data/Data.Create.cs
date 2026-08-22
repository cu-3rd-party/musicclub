using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Infrastructure.Data;

namespace CuMusicClub.Web.Endpoints.v1.Data;

public static partial class Data
{
    private static async Task<IResult> Create(ApplicationDbContext db,
        IDataEntryService dataEntryService,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // наверное стоит ограничить лимит загрузки файлов, ато этот бекенд можно использовать как файлообменник
        if (file.Length == 0) return TypedResults.BadRequest("File is empty.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var content = stream.ToArray();
        var entry = await dataEntryService.Create(content, file.ContentType, cancellationToken);

        return TypedResults.Created($"/api/v1/data/{entry.Id}", entry.Id);
    }
}
