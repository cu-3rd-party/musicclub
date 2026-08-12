using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.Song.Helpers;
using CuMusicClub.Domain.Enums;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Helpers;

[TestFixture]
[TestOf(typeof(SongHelpers))]
public class SongHelpersTests
{
    [TestFixture]
    public class DeriveLinkKindTests
    {
        [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ", SongLinkType.Youtube)]
        [TestCase("https://youtube.com/watch?v=abc", SongLinkType.Youtube)]
        [TestCase("https://m.youtube.com/watch?v=abc", SongLinkType.Youtube)]
        [TestCase("https://youtu.be/dQw4w9WgXcQ", SongLinkType.Youtube)]
        [TestCase("https://music.yandex.ru/album/123/track/456", SongLinkType.YandexMusic)]
        [TestCase("https://music.yandex.com/album/123", SongLinkType.YandexMusic)]
        [TestCase("https://yandex.ru/music/album/123", SongLinkType.YandexMusic)]
        [TestCase("https://soundcloud.com/artist/track", SongLinkType.Soundcloud)]
        public void ReturnsCorrectLinkType(string url, SongLinkType expected)
        {
            SongHelpers
                .DeriveLinkKind(url)
                .ShouldBe(expected);
        }

        [Test]
        public void TrimsAndCaseInsensitive()
        {
            SongHelpers
                .DeriveLinkKind("  https://YOUTUBE.COM/watch?v=abc  ")
                .ShouldBe(SongLinkType.Youtube);
        }

        [Test]
        public void NullUrl_ThrowsValidationException()
        {
            Should.Throw<ValidationException>(() => SongHelpers.DeriveLinkKind(null!));
        }

        [Test]
        public void EmptyUrl_ThrowsValidationException()
        {
            Should.Throw<ValidationException>(() => SongHelpers.DeriveLinkKind(""));
        }

        [Test]
        public void WhitespaceUrl_ThrowsValidationException()
        {
            Should.Throw<ValidationException>(() => SongHelpers.DeriveLinkKind("   "));
        }

        [Test]
        public void UnsupportedUrl_ThrowsValidationException()
        {
            Should.Throw<ValidationException>(() => SongHelpers.DeriveLinkKind("https://example.com/song.mp3"));
        }

        [Test]
        public void UnsupportedUrl_ErrorMessageContainsUrl()
        {
            var ex =
                Should.Throw<ValidationException>(() => SongHelpers.DeriveLinkKind("https://spotify.com/track/123"));
            ex.Errors.ShouldContainKey("url");
        }
    }

    [TestFixture]
    public class NormalizeRolesTests
    {
        [Test]
        public void NullInput_ReturnsEmptyList()
        {
            SongHelpers
                .NormalizeRoles(null)
                .ShouldBeEmpty();
        }

        [Test]
        public void EmptyInput_ReturnsEmptyList()
        {
            SongHelpers
                .NormalizeRoles(Array.Empty<string>())
                .ShouldBeEmpty();
        }

        [Test]
        public void TrimsWhitespace()
        {
            var result = SongHelpers.NormalizeRoles(new[]
            {
                "  Vocal  ",
                "  Guitar  ",
            });
            result.ShouldBe(new[]
            {
                "Guitar",
                "Vocal",
            });
        }

        [Test]
        public void DropsEmptyEntries()
        {
            var result = SongHelpers.NormalizeRoles(new[]
            {
                "Vocal",
                "",
                "  ",
                "Guitar",
            });
            result.ShouldBe(new[]
            {
                "Guitar",
                "Vocal",
            });
        }

        [Test]
        public void DeduplicatesOrdinal()
        {
            var result = SongHelpers.NormalizeRoles(new[]
            {
                "Vocal",
                "Vocal",
                "Vocal",
            });
            result.Count.ShouldBe(1);
            result[0]
                .ShouldBe("Vocal");
        }

        [Test]
        public void CaseSensitive_DoesNotDeduplicate()
        {
            var result = SongHelpers.NormalizeRoles(new[]
            {
                "vocal",
                "Vocal",
                "VOCAL",
            });
            result.Count.ShouldBe(3);
        }

        [Test]
        public void SortsAlphabetically()
        {
            var result = SongHelpers.NormalizeRoles(new[]
            {
                "Drums",
                "Bass",
                "Guitar",
                "Vocal",
            });
            result.ShouldBe(new[]
            {
                "Bass",
                "Drums",
                "Guitar",
                "Vocal",
            });
        }

        [Test]
        public void SingleRole()
        {
            SongHelpers
                .NormalizeRoles(new[]
                {
                    "Vocal",
                })
                .ShouldBe(new[]
                {
                    "Vocal",
                });
        }

        [Test]
        public void AllEmpty_ReturnsEmptyList()
        {
            SongHelpers
                .NormalizeRoles(new[]
                {
                    "",
                    "  ",
                    "",
                })
                .ShouldBeEmpty();
        }
    }
}
