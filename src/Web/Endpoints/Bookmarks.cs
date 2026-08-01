using Microsoft.AspNetCore.Http.HttpResults;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Web.Endpoints;

public class Bookmarks : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.MapPost("/", CreateBookmark);
        group.MapGet("/{uid}", GetBookmark);
    }

    [EndpointSummary("Create a bookmark")]
    public static async Task<Created<BookmarkCreatedResponse>> CreateBookmark(
        IApplicationDbContext context,
        CreateBookmarkRequest request)
    {
        var uid = Guid.NewGuid();
        context.Add(new Bookmark
        {
            Id = uid, Title = request.Title, Url = request.Url, Description = request.Description,
        });
        await context.SaveChangesAsync(default);
        return TypedResults.Created($"/api/Bookmarks/{uid}", new BookmarkCreatedResponse(uid));
    }

    [EndpointSummary("Get a bookmark by uid")]
    public static async Task<Results<Ok<BookmarkResponse>, NotFound>> GetBookmark(
        Guid uid,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var bookmark = await context
            .Bookmarks
            .FirstOrDefaultAsync(x => x.Id == uid, cancellationToken);

        if (bookmark == null) return TypedResults.NotFound();

        return TypedResults.Ok(new BookmarkResponse(
            bookmark.Title,
            bookmark.Url,
            bookmark.Description
        ));
    }
}

public record CreateBookmarkRequest(string Title, string Url, string? Description);

public record BookmarkCreatedResponse(Guid uid);

public record BookmarkResponse(string Title, string Url, string? Description);
