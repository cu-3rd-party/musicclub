using System.Security.Claims;
using System.Text.Encodings.Web;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.DTOs.Calendar;
using CuMusicClub.Application.Services.Calendar;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CuMusicClub.Web.Endpoints.v1.Calendar;

public static partial class Calendar
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/feed", GetFeed);
        group.MapPost("/feed/regenerate", RegenerateFeed);
        group.MapDelete("/feed", RevokeFeed);
        group.MapGet("/events", GetEvents);
        group.MapPost("/events", CreateEvent);
        group.MapDelete("/events/{eventId:guid}", DeleteEvent);
    }

    [EndpointSummary("Get your calendar feed")]
    private static async Task<Ok<CalendarFeedDto>> GetFeed(
        HttpContext httpContext,
        ClaimsPrincipal claimsPrincipal,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        var feed = await service.GetOrCreateFeedAsync(userId, cancellationToken);
        feed.IcsUrl = BuildIcsUrl(httpContext, feed.FeedToken);
        return TypedResults.Ok(feed);
    }

    [EndpointSummary("Regenerate your calendar feed token")]
    private static async Task<Ok<CalendarFeedDto>> RegenerateFeed(
        HttpContext httpContext,
        ClaimsPrincipal claimsPrincipal,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        var feed = await service.RegenerateTokenAsync(userId, cancellationToken);
        feed.IcsUrl = BuildIcsUrl(httpContext, feed.FeedToken);
        return TypedResults.Ok(feed);
    }

    [EndpointSummary("Revoke your calendar feed")]
    private static async Task<Ok<CalendarFeedDto>> RevokeFeed(
        HttpContext httpContext,
        ClaimsPrincipal claimsPrincipal,
        ICalendarService service,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        var feed = await service.RevokeFeedAsync(userId, cancellationToken);
        feed.IcsUrl = BuildIcsUrl(httpContext, feed.FeedToken);
        return TypedResults.Ok(feed);
    }

    [EndpointSummary("Get your calendar events")]
    private static async Task<Ok<List<CalendarEventDto>>> GetEvents(
        ClaimsPrincipal claimsPrincipal,
        ICalendarService service,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        var events = await service.GetEventsAsync(userId, from, to, cancellationToken);
        return TypedResults.Ok(events);
    }

    [EndpointSummary("Create a personal calendar event")]
    private static async Task<Created<CalendarEventDto>> CreateEvent(
        ClaimsPrincipal claimsPrincipal,
        ICalendarService service,
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        var userId = claimsPrincipal.GetUserId();
        request.UserId = userId;

        var calendarEvent = await service.CreateEventAsync(request, cancellationToken);

        return TypedResults.Created($"/api/v1/calendar/events/{calendarEvent.Id}", calendarEvent);
    }

    [EndpointSummary("Delete a calendar event")]
    private static async Task<NoContent> DeleteEvent(
        ICalendarService service,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await service.DeleteEventAsync(eventId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static string BuildIcsUrl(HttpContext httpContext, string token)
    {
        var request = httpContext.Request;
        var scheme = request.Scheme;
        var host = request.Host.Value;
        var pathBase = request.PathBase.Value;
        return $"{scheme}://{host}{pathBase}/ics/{token}.ics";
    }
}
