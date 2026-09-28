namespace CuMusicClub.Infrastructure.YandexCalDav;

internal sealed record Response(int StatusCode, string Method, string Content)
{
    public bool IsSuccessful
    {
        get { return StatusCode is >= 200 and <= 299; }
    }

    public override string ToString()
    {
        return $"${Method} CalDAV response - Status code: {StatusCode}";
    }
}
