using CuMusicClub.Infrastructure.Data;

namespace CuMusicClub.Web.Endpoints.v1.Data;

public static partial class Data
{
    private static async Task<IResult> Get(ApplicationDbContext db, Guid dataId, CancellationToken cancellationToken)
    {
        var entry = await db.DataEntries.FirstOrDefaultAsync(d => d.Id == dataId, cancellationToken);

        if (entry == null) return TypedResults.NotFound();

        return TypedResults.File(entry.Content, entry.ContentType, enableRangeProcessing: true);
    }
}
