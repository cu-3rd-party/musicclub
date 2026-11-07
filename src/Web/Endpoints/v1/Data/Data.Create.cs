using CuMusicClub.Application.Services.DataEntry;

namespace CuMusicClub.Web.Endpoints.v1.Data;

public static partial class Data
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static async Task<IResult> Create(IDataEntryService dataEntryService,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0) return ApiProblems.BadRequest("Файл пустой.");
        if (file.Length > MaxFileSizeBytes) return ApiProblems.BadRequest($"Файл больше {MaxFileSizeBytes / (1024 * 1024)} МБ — выберите поменьше.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var content = stream.ToArray();
        var entry = await dataEntryService.Create(content, file.ContentType, cancellationToken);

        return TypedResults.Created($"/api/v1/data/{entry.Id}", entry.Id);
    }
}
