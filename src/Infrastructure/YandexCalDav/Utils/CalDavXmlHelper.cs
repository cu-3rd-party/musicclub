using System.Text;
using System.Xml;
using System.Xml.Linq;
using CuMusicClub.Infrastructure.YandexCalDav.Constants;
using CuMusicClub.Infrastructure.YandexCalDav.Models;

namespace CuMusicClub.Infrastructure.YandexCalDav.Utils;

internal static class CalDavXmlHelper
{
    public static string CreatePropFindXml(params string[] properties)
    {
        var doc = new XmlDocument();
        var propfindElement = CreateElementWithNamespace(doc, CalDavXmlElements.PropFind, Namespaces.DavNamespace);
        doc.AppendChild(propfindElement);

        if (properties.Length > 0)
        {
            var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
            propfindElement.AppendChild(propElement);

            foreach (var property in properties)
            {
                var propertyElement = CreatePropertyElement(doc, property);
                propElement.AppendChild(propertyElement);
            }
        }
        else
        {
            var allPropElement = CreateElementWithNamespace(doc, CalDavXmlElements.AllProp, Namespaces.DavNamespace);
            propfindElement.AppendChild(allPropElement);
        }

        return FormatXmlDocument(doc);
    }

    public static string CreateCalendarQueryXml(DateTime? startDate = null, DateTime? endDate = null,
        params string[] properties)
    {
        var doc = new XmlDocument();
        var queryElement = CreateElementWithNamespace(doc, CalDavXmlElements.CalendarQuery, Namespaces.CalDavNamespace);
        doc.AppendChild(queryElement);

        if (properties.Length > 0)
        {
            var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
            queryElement.AppendChild(propElement);

            foreach (var property in properties)
            {
                var propertyElement = CreatePropertyElement(doc, property);
                propElement.AppendChild(propertyElement);
            }
        }

        if (startDate.HasValue || endDate.HasValue)
        {
            var filterElement =
                CreateElementWithNamespace(doc, CalDavXmlElements.Filter, Namespaces.CalDavNamespace);
            queryElement.AppendChild(filterElement);

            var compFilterElement =
                CreateElementWithNamespace(doc, CalDavXmlElements.CompFilter, Namespaces.CalDavNamespace);
            compFilterElement.SetAttribute("name", Components.VCalendar);
            filterElement.AppendChild(compFilterElement);

            var eventCompFilterElement =
                CreateElementWithNamespace(doc, CalDavXmlElements.CompFilter, Namespaces.CalDavNamespace);
            eventCompFilterElement.SetAttribute("name", Components.VEvent);
            compFilterElement.AppendChild(eventCompFilterElement);

            var timeRangeElement =
                CreateElementWithNamespace(doc, CalDavXmlElements.TimeRange, Namespaces.CalDavNamespace);

            if (startDate.HasValue) timeRangeElement.SetAttribute("start", FormatDateTimeForCalDav(startDate.Value));

            if (endDate.HasValue) timeRangeElement.SetAttribute("end", FormatDateTimeForCalDav(endDate.Value));

            eventCompFilterElement.AppendChild(timeRangeElement);
        }

        return FormatXmlDocument(doc);
    }

    public static string CreateCalendarMultiGetXml(IEnumerable<string> eventUrls, params string[] properties)
    {
        var doc = new XmlDocument();
        var multigetElement =
            CreateElementWithNamespace(doc, CalDavXmlElements.CalendarMultiGet, Namespaces.CalDavNamespace);
        doc.AppendChild(multigetElement);

        if (properties.Length > 0)
        {
            var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
            multigetElement.AppendChild(propElement);

            foreach (var property in properties)
            {
                var propertyElement = CreatePropertyElement(doc, property);
                propElement.AppendChild(propertyElement);
            }
        }

        foreach (var url in eventUrls)
        {
            var hrefElement = CreateElementWithNamespace(doc, CalDavXmlElements.Href, Namespaces.DavNamespace);
            hrefElement.InnerText = url;
            multigetElement.AppendChild(hrefElement);
        }

        return FormatXmlDocument(doc);
    }

    public static string CreateMkCalendarXml(string displayName, string? color = null)
    {
        var doc = new XmlDocument();
        var mkcalendarElement =
            CreateElementWithNamespace(doc, CalDavXmlElements.MkCalendar, Namespaces.CalDavNamespace);
        doc.AppendChild(mkcalendarElement);

        var setElement = CreateElementWithNamespace(doc, "set", Namespaces.DavNamespace);
        mkcalendarElement.AppendChild(setElement);

        var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
        setElement.AppendChild(propElement);

        var displayNameElement = CreateElementWithNamespace(doc, Properties.DisplayName, Namespaces.DavNamespace);
        displayNameElement.InnerText = displayName;
        propElement.AppendChild(displayNameElement);

        var resourceTypeElement = CreateElementWithNamespace(doc, Properties.ResourceType, Namespaces.DavNamespace);
        var collectionElement = CreateElementWithNamespace(doc, "collection", Namespaces.DavNamespace);
        var calendarElement = CreateElementWithNamespace(doc, "calendar", Namespaces.CalDavNamespace);
        resourceTypeElement.AppendChild(collectionElement);
        resourceTypeElement.AppendChild(calendarElement);
        propElement.AppendChild(resourceTypeElement);

        var supportedComponentsElement =
            CreateElementWithNamespace(doc, Properties.SupportedCalendarComponentSet, Namespaces.CalDavNamespace);
        var veventCompElement = CreateElementWithNamespace(doc, "comp", Namespaces.CalDavNamespace);
        veventCompElement.SetAttribute("name", Components.VEvent);
        supportedComponentsElement.AppendChild(veventCompElement);
        propElement.AppendChild(supportedComponentsElement);

        if (!string.IsNullOrWhiteSpace(color))
        {
            var colorElement = CreateElementWithNamespace(doc, Properties.CalendarColor, Namespaces.AppleNamespace);
            colorElement.InnerText = color;
            propElement.AppendChild(colorElement);
        }

        return FormatXmlDocument(doc);
    }

    public static List<CalendarInfo> ParseCalendarListResponse(string xmlResponse)
    {
        var calendars = new List<CalendarInfo>();
        var doc = new XmlDocument();
        doc.LoadXml(xmlResponse);

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace(NamespacesAliases.DavNamespace, Namespaces.DavNamespace);
        namespaceManager.AddNamespace(NamespacesAliases.CalDavNamespace, Namespaces.CalDavNamespace);
        namespaceManager.AddNamespace(NamespacesAliases.AppleNamespace, Namespaces.AppleNamespace);

        var responseNodes = doc.SelectNodes($"//{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Response}",
            namespaceManager);
        if (responseNodes == null) return calendars;

        foreach (XmlNode responseNode in responseNodes)
        {
            var calendar = ParseCalendarFromResponse(responseNode, namespaceManager);
            if (calendar != null) calendars.Add(calendar);
        }

        return calendars;
    }

    public static string CreateCurrentUserPrincipalXml()
    {
        var doc = new XmlDocument();
        var propfindElement = CreateElementWithNamespace(doc, CalDavXmlElements.PropFind, Namespaces.DavNamespace);
        doc.AppendChild(propfindElement);

        var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
        propfindElement.AppendChild(propElement);

        var currentUserPrincipalElement =
            CreateElementWithNamespace(doc, Properties.CurrentUserPrincipal, Namespaces.DavNamespace);
        propElement.AppendChild(currentUserPrincipalElement);

        return FormatXmlDocument(doc);
    }

    public static string CreateCalendarHomeSetXml()
    {
        var doc = new XmlDocument();
        var propfindElement = CreateElementWithNamespace(doc, CalDavXmlElements.PropFind, Namespaces.DavNamespace);
        doc.AppendChild(propfindElement);

        var propElement = CreateElementWithNamespace(doc, CalDavXmlElements.Prop, Namespaces.DavNamespace);
        propfindElement.AppendChild(propElement);

        var calendarHomeSetElement =
            CreateElementWithNamespace(doc, Properties.CalendarHomeSet, Namespaces.CalDavNamespace);
        propElement.AppendChild(calendarHomeSetElement);

        return FormatXmlDocument(doc);
    }

    public static string? ParseCurrentUserPrincipalResponse(string xmlResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlResponse);

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace(NamespacesAliases.DavNamespace, Namespaces.DavNamespace);

        var hrefNode =
            doc.SelectSingleNode(
                $"//{NamespacesAliases.DavNamespace}:{Properties.CurrentUserPrincipal}/{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Href}",
                namespaceManager);
        return hrefNode?.InnerText;
    }

    public static string? ParseCalendarHomeSetResponse(string xmlResponse)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlResponse);

        var namespaceManager = new XmlNamespaceManager(doc.NameTable);
        namespaceManager.AddNamespace(NamespacesAliases.DavNamespace, Namespaces.DavNamespace);
        namespaceManager.AddNamespace(NamespacesAliases.CalDavNamespace, Namespaces.CalDavNamespace);

        var hrefNode =
            doc.SelectSingleNode(
                $"//{NamespacesAliases.CalDavNamespace}:{Properties.CalendarHomeSet}/{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Href}",
                namespaceManager);
        return hrefNode?.InnerText;
    }

    internal static StringContent ToStringContent(this XDocument document)
    {
        return new StringContent(document.ToString(), Encoding.UTF8, ContentTypes.XmlContentType);
    }

    private static XmlElement CreateElementWithNamespace(XmlDocument doc, string localName, string namespaceUri)
    {
        return doc.CreateElement(localName, namespaceUri);
    }

    private static XmlElement CreatePropertyElement(XmlDocument doc, string propertyName)
    {
        return propertyName switch
        {
            Properties.CalendarData => CreateElementWithNamespace(doc, Properties.CalendarData,
                Namespaces.CalDavNamespace),
            Properties.GeTeTag => CreateElementWithNamespace(doc, Properties.GeTeTag, Namespaces.DavNamespace),
            Properties.DisplayName => CreateElementWithNamespace(doc, Properties.DisplayName, Namespaces.DavNamespace),
            Properties.ResourceType =>
                CreateElementWithNamespace(doc, Properties.ResourceType, Namespaces.DavNamespace),
            Properties.CalendarColor => CreateElementWithNamespace(doc, Properties.CalendarColor,
                Namespaces.AppleNamespace),
            Properties.SupportedCalendarComponentSet => CreateElementWithNamespace(doc,
                Properties.SupportedCalendarComponentSet, Namespaces.CalDavNamespace),
            Properties.CurrentUserPrincipal => CreateElementWithNamespace(doc, Properties.CurrentUserPrincipal,
                Namespaces.DavNamespace),
            Properties.CalendarHomeSet => CreateElementWithNamespace(doc, Properties.CalendarHomeSet,
                Namespaces.CalDavNamespace),

            _ => CreateElementWithNamespace(doc, propertyName, Namespaces.DavNamespace)
        };
    }

    private static string FormatXmlDocument(XmlDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings)) doc.WriteTo(writer);

        return sb.ToString();
    }

    private static string FormatDateTimeForCalDav(DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
    }

    private static CalendarInfo? ParseCalendarFromResponse(XmlNode responseNode, XmlNamespaceManager namespaceManager)
    {
        var hrefNode = responseNode.SelectSingleNode($"{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Href}",
            namespaceManager);
        if (hrefNode == null) return null;

        var propstatNode = responseNode.SelectSingleNode(
            $"{NamespacesAliases.DavNamespace}:{CalDavXmlElements.PropStat}[{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Status}[contains(text(), '200')]]",
            namespaceManager);
        if (propstatNode == null) return null;

        var propNode = propstatNode.SelectSingleNode($"{NamespacesAliases.DavNamespace}:{CalDavXmlElements.Prop}",
            namespaceManager);
        if (propNode == null) return null;

        var resourceTypeNode =
            propNode.SelectSingleNode($"{NamespacesAliases.DavNamespace}:{Properties.ResourceType}/c:calendar",
                namespaceManager);
        if (resourceTypeNode == null) return null;

        var displayNameNode = propNode.SelectSingleNode($"{NamespacesAliases.DavNamespace}:{Properties.DisplayName}",
            namespaceManager);
        var colorNode = propNode.SelectSingleNode($"{NamespacesAliases.AppleNamespace}:{Properties.CalendarColor}",
            namespaceManager);
        var etagNode =
            propNode.SelectSingleNode($"{NamespacesAliases.DavNamespace}:{Properties.GeTeTag}", namespaceManager);
        return new CalendarInfo(hrefNode.InnerText, displayNameNode?.InnerText, colorNode?.InnerText,
            etagNode?.InnerText);
    }
}
