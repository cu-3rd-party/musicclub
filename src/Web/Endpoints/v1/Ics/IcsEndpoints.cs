using System.Text;
using CuMusicClub.Application.Services.Calendar;
using Microsoft.AspNetCore.Http;

namespace CuMusicClub.Web.Endpoints.v1.Ics;

public static class IcsEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/ics/{token}.ics", GetIcsFeed);
    }

    [EndpointSummary("Get public ICS calendar feed by token")]
    [EndpointDescription("Returns an ICS file for calendar subscription. Requires a valid feed token.")]
    private static async Task<IResult> GetIcsFeed(
        string token,
        ICalendarService calendarService,
        IIcsFeedGenerator icsFeedGenerator,
        CancellationToken cancellationToken)
    {
        var feed = await calendarService.GetFeedByTokenAsync(token, cancellationToken);

        if (feed == null || !feed.IsActive)
        {
            return Results.NotFound();
        }

        // Получаем события: последние 30 дней и следующие 90 дней
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow.AddDays(90);

        var events = await calendarService.GetEventsAsync(feed.UserId, from, to, cancellationToken);

        var icsContent = icsFeedGenerator.GenerateIcs(
            events,
            "-//Music Club ERP//Calendar//RU",
            "Music Club Calendar");

        return new PlainTextContentResult(
            icsContent,
            "text/calendar; charset=utf-8",
            Encoding.UTF8,
            cacheControl: "public, max-age=300",
            contentDisposition: "attachment; filename=\"calendar.ics\"");
    }

    /// <summary>
    /// Результат с простым текстом и кастомными заголовками.
    /// </summary>
    private sealed class PlainTextContentResult : IResult
    {
        private readonly string _content;
        private readonly string _contentType;
        private readonly Encoding _encoding;
        private readonly string? _cacheControl;
        private readonly string? _contentDisposition;

        public PlainTextContentResult(
            string content,
            string contentType,
            Encoding encoding,
            string? cacheControl = null,
            string? contentDisposition = null)
        {
            _content = content;
            _contentType = contentType;
            _encoding = encoding;
            _cacheControl = cacheControl;
            _contentDisposition = contentDisposition;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = _contentType;

            if (!string.IsNullOrEmpty(_cacheControl))
            {
                httpContext.Response.Headers.CacheControl = _cacheControl;
            }

            if (!string.IsNullOrEmpty(_contentDisposition))
            {
                httpContext.Response.Headers.ContentDisposition = _contentDisposition;
            }

            await httpContext.Response.WriteAsync(_content, _encoding);
        }
    }
}
