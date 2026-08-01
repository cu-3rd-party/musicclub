using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Web.Endpoints;

public class ShortenedUrls : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.MapPost("/", CreateShortenedUrl);
    }

    [EndpointSummary("Create a shortened URL")]
    public static async Task<Created<string>> CreateShortenedUrl(
        IApplicationDbContext context,
        CreateShortenedUrlRequest request,
        HttpContext httpContext)
    {
        var code = GenerateCode();
        context.Add(new ShortenedUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = request.Url,
            ShortCode = code,
            Created = DateTimeOffset.UtcNow,
            CreatedBy = httpContext.User.Identity?.Name
        });
        await context.SaveChangesAsync(default);
        return TypedResults.Created($"/{code}", code);
    }

    [EndpointSummary("Redirect to original URL")]
    public static async Task<IResult> RedirectUrl(
        string code, IApplicationDbContext context, CancellationToken cancellationToken)
    {
        var shortened = await context.ShortenedUrls
            .FirstOrDefaultAsync(x => x.ShortCode == code, cancellationToken);
        return shortened is null
            ? Results.NotFound()
            : Results.Redirect(shortened.OriginalUrl);
    }

    private static string GenerateCode()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, 8).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }
}

public record CreateShortenedUrlRequest(string Url);
