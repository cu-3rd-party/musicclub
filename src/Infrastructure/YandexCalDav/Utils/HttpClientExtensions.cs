using System.Text;
using System.Xml.Linq;

namespace CuMusicClub.Infrastructure.YandexCalDav.Utils;

internal static class HttpClientExtensions
{
    public static async Task<Response> SendCalDavRequestAsync(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        var encoding = GetEncoding(response.Content, Encoding.UTF8);
        var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var result = new Response((int) response.StatusCode, request.Method.Method, encoding.GetString(contentBytes));
        return result;
    }

    public static HttpRequestMessage WithContent(this HttpRequestMessage request, HttpContent content)
    {
        request.Content = content;

        return request;
    }

    public static HttpRequestMessage WithXmlContent(this HttpRequestMessage request, XElement root)
    {
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", null));
        document.Add(root);

        return request.WithContent(document.ToStringContent());
    }

    public static HttpRequestMessage WithHeader(this HttpRequestMessage request, string name, string value)
    {
        request.Headers.Add(name, value);

        return request;
    }

    private static Encoding GetEncoding(HttpContent content, Encoding defaultEncoding)
    {
        if (content.Headers.ContentType?.CharSet == null) return defaultEncoding;

        try
        {
            return Encoding.GetEncoding(content.Headers.ContentType.CharSet);
        }
        catch
        {
            return defaultEncoding;
        }
    }
}
