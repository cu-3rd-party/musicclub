using CuMusicClub.Application.Services.DataEntry;

namespace CuMusicClub.Web.Endpoints.v1.Data;

public static partial class Data
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static async Task<IResult> Create(IDataEntryService dataEntryService,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0) return TypedResults.BadRequest("File is empty.");
        if (file.Length > MaxFileSizeBytes) return TypedResults.BadRequest($"File size exceeds maximum of {MaxFileSizeBytes / (1024 * 1024)}MB.");
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var content = stream.ToArray();
        var entry = await dataEntryService.Create(content, file.ContentType, cancellationToken);

        return TypedResults.Created($"/api/v1/data/{entry.Id}", entry.Id);
    }
}
