using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CuMusicClub.Infrastructure.YandexCalDav.Config;
using CuMusicClub.Infrastructure.YandexCalDav.Constants;
using CuMusicClub.Infrastructure.YandexCalDav.Exceptions;
using CuMusicClub.Infrastructure.YandexCalDav.Models;
using CuMusicClub.Infrastructure.YandexCalDav.Utils;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Options;
using NotFoundException = CuMusicClub.Infrastructure.YandexCalDav.Exceptions.NotFoundException;

namespace CuMusicClub.Infrastructure.YandexCalDav;

internal sealed class YandexCalDavClient : IYandexCalDavClient
{
    private readonly YandexCaldavConfig _config;

    private readonly string _homeSetPath;
    private readonly HttpClient _httpClient;

    public YandexCalDavClient(IHttpClientFactory httpClientFactory, IOptions<YandexCaldavConfig> config)
    {
        _config = config.Value;
        _homeSetPath = string.Format(null, PathConstants.HomeSetPathFormat, _config.User);
        _httpClient = httpClientFactory.CreateClient();

        SetupAuthentication();
    }

    public string Organizer
    {
        get { return _config.User; }
    }

    public async Task<List<CalendarInfo>> GetCalendarsAsync()
    {
        var xmlString = CalDavXmlHelper.CreatePropFindXml(Properties.DisplayName, Properties.ResourceType);
        var xml = XElement.Parse(xmlString);

        using var request = new HttpRequestMessage(new HttpMethod(Methods.PropFind), GetFullUrl(_homeSetPath))
            .WithXmlContent(xml)
            .WithHeader("Depth", "1");

        var response = await _httpClient.SendCalDavRequestAsync(request);
        if (!response.IsSuccessful)
            throw new InvalidOperationException($"Failed to get calendars: {response.StatusCode}");

        return CalDavXmlHelper.ParseCalendarListResponse(response.Content);
    }

    public async Task<CalendarInfo> GetCalendarAsync(string calendarUrl)
    {
        var fullUrl = GetFullUrl(calendarUrl);
        var propFindXml = XElement.Parse(CalDavXmlHelper.CreatePropFindXml(
            Properties.DisplayName,
            Properties.ResourceType,
            Properties.CalendarColor,
            Properties.GeTeTag));

        using var request = new HttpRequestMessage(new HttpMethod(Methods.PropFind), fullUrl)
            .WithXmlContent(propFindXml)
            .WithHeader("Depth", "0");
        var response = await _httpClient.SendCalDavRequestAsync(request);
        if (!response.IsSuccessful)
            throw new InvalidOperationException($"Failed to get calendar: {response.StatusCode}");

        var calendars = CalDavXmlHelper.ParseCalendarListResponse(response.Content);

        return calendars.Count == 1 ? calendars[0] : throw new NotFoundException("Calendar not found.");
    }

    public async Task<CalendarInfo> CreateCalendarAsync(string displayName, string? color = null,
        CancellationToken cancellationToken = default)
    {
        var calendarsBefore = await GetCalendarsAsync();
        if (calendarsBefore.Exists(c => c.DisplayName == displayName))
            throw new DuplicateObjectException("Calendar names should be unique.");

        var calendarUrl = GetFullUrl(_homeSetPath + "new");
        var xmlString = CalDavXmlHelper.CreateMkCalendarXml(displayName, color);
        var xml = XElement.Parse(xmlString);
        using var request = new HttpRequestMessage(new HttpMethod(Methods.MkCalendar), calendarUrl)
            .WithXmlContent(xml);
        await _httpClient.SendCalDavRequestAsync(request, cancellationToken);

        var calendarsAfter = await GetCalendarsAsync();

        var calendarUrlsBefore = calendarsBefore.Select(c => c.Url);
        var calendarUrlsAfter = calendarsAfter.Select(c => c.Url);
        var diff = calendarUrlsAfter.Except(calendarUrlsBefore).ToHashSet();

        if (diff.Count != 1)
            throw new ConcurrentAccessException($"Expected to create exactly one calendar but was {diff.Count}");

        return calendarsAfter.Single(c => c.Url == diff.Single());
    }

    public async Task<List<CalendarEventInfo>> GetEventsAsync(string calendarUrl, DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var fullUrl = GetFullUrl(calendarUrl);
        var queryXml =
            XElement.Parse(CalDavXmlHelper.CreateCalendarQueryXml(startDate, endDate, Properties.CalendarData,
                Properties.GeTeTag));

        using var request = new HttpRequestMessage(new HttpMethod(Methods.Report), fullUrl)
            .WithXmlContent(queryXml);
        var response = await _httpClient.SendCalDavRequestAsync(request);

        return ParseEventsFromResponse(response.Content);
    }

    public async Task<CalendarEventInfo?> GetEventAsync(string eventUrl)
    {
        var fullUrl = GetFullUrl(eventUrl);
        var response = await _httpClient.GetStringAsync(fullUrl);
        return CalendarEventInfo.FromIcal(response, eventUrl);
    }

    public async Task<CalendarEventInfo> CreateEventAsync(string calendarUrl, CalendarEventInfo eventInfo)
    {
        var eventHref = $"{calendarUrl}{eventInfo.Uid}.ics";

        var eventUrl = GetFullUrl(eventHref);

        var iCalData = eventInfo.ToIcal(GetOrganizer());

        using var content = new StringContent(iCalData, Encoding.UTF8, ContentTypes.CalendarContentType);
        var response = await _httpClient.PutAsync(eventUrl, content);
        response.EnsureSuccessStatusCode();

        eventInfo.SetEtag(response.Headers.ETag?.Tag);
        eventInfo.SetHref(eventHref);
        return eventInfo;
    }

    public async Task<CalendarEventInfo> UpdateEventAsync(CalendarEventInfo eventInfo)
    {
        if (!eventInfo.IsModified) return eventInfo;

        if (string.IsNullOrEmpty(eventInfo.Href))
            throw new ArgumentException($"Event without {nameof(eventInfo.Href)} could not be updated.");

        var iCalData = eventInfo.ToIcal(GetOrganizer());
        using var content = new StringContent(iCalData, Encoding.UTF8, ContentTypes.CalendarContentType);

        if (!string.IsNullOrWhiteSpace(eventInfo.ETag)) content.Headers.Add("If-Match", eventInfo.ETag);

        var response = await _httpClient.PutAsync(GetFullUrl(eventInfo.Href), content);
        response.EnsureSuccessStatusCode();

        eventInfo.SetEtag(response.Headers.ETag?.Tag);
        eventInfo.SetModified(false);
        return eventInfo;
    }

    public async Task DeleteEventAsync(string eventUrl)
    {
        var fullUrl = GetFullUrl(eventUrl);
        var response = await _httpClient.DeleteAsync(fullUrl);
        response.EnsureSuccessStatusCode();
    }

    private Organizer GetOrganizer()
    {
        return new Organizer { CommonName = _config.OrganizerCN, Value = new Uri($"mailto:{_config.User}") };
    }

    private void SetupAuthentication()
    {
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.User}:{_config.Password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    private Uri GetFullUrl(string url)
    {
        var uriString = Uri.IsWellFormedUriString(url, UriKind.Absolute) ? url :
            url.StartsWith('/') ? _config.BaseUrl + url : $"{_config.BaseUrl}/{url}";

        return new Uri(uriString);
    }

    private static List<CalendarEventInfo> ParseEventsFromResponse(string xmlResponse)
    {
        var allEvents = new List<CalendarEventInfo>();
        var mainEvents = new Dictionary<string, CalendarEventInfo>();
        var exceptions = new List<CalendarEventInfo>();

        var doc = new XmlDocument();
        doc.LoadXml(xmlResponse);
        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace(NamespacesAliases.DavNamespace, Namespaces.DavNamespace);
        namespaceManager.AddNamespace(NamespacesAliases.CalDavNamespace, Namespaces.CalDavNamespace);

        var responses = doc.SelectNodes($"//{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Response}",
            namespaceManager);
        if (responses == null || responses.Count == 0) return allEvents;

        foreach (XmlNode response in responses) ProcessResponseNode(response, namespaceManager, mainEvents, exceptions);

        foreach (var gr in exceptions.GroupBy(e => e.Uid))
            if (mainEvents.TryGetValue(gr.Key, out var mainEvent))
                mainEvent.SetRecurrenceExceptions(gr.ToArray());
            else
                allEvents.AddRange(gr.ToArray());

        allEvents.AddRange(mainEvents.Values);
        return allEvents;
    }

    private static void ProcessResponseNode(
        XmlNode responseNode,
        XmlNamespaceManager namespaceManager,
        Dictionary<string, CalendarEventInfo> mainEvents,
        List<CalendarEventInfo> exceptions)
    {
        var href = responseNode
            .SelectSingleNode($"./{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Href}", namespaceManager)
            ?.InnerText;
        var calendarDataNode = responseNode.SelectSingleNode(
            $"./{NamespacesAliases.DavNamespace}:{CalDavXmlElements.PropStat}/{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Prop}/{NamespacesAliases.CalDavNamespace}:{Properties.CalendarData}",
            namespaceManager);
        var iCalData = calendarDataNode?.InnerText;
        if (iCalData == null || href == null) return;

        var calendar = Calendar.Load(iCalData);
        foreach (var calendarEvent in calendar.Events)
        {
            var evt = new CalendarEventInfo(calendarEvent, href);

            if (evt.IsRecurrenceException)
                exceptions.Add(evt);
            else
                mainEvents[evt.Uid] = evt;
        }
    }
}
