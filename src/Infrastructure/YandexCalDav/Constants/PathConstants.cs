using System.Text;

namespace CuMusicClub.Infrastructure.YandexCalDav.Constants;

internal static class PathConstants
{
    public static readonly CompositeFormat HomeSetPathFormat = CompositeFormat.Parse("/calendars/{0}/");
}
