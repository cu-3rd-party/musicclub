using CuMusicClub.Domain.Enums;
using CuMusicClub.Domain.ValueObjects;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.ValueObjects;

[TestFixture]
[TestOf(typeof(SongThumbnail))]
public class SongThumbnailTests
{
    [TestFixture]
    public class NormalizeTests
    {
        [Test]
        public void CustomUrl_ReturnsCustomUrl()
        {
            SongThumbnail.Normalize("https://custom.com/thumb.jpg",
                    SongLinkType.Youtube,
                    "https://youtube.com/watch?v=abc")
                .ShouldBe("https://custom.com/thumb.jpg");
        }

        [Test]
        public void NullCustomUrl_YoutubeLink_ReturnsThumbnail()
        {
            var result =
                SongThumbnail.Normalize(null, SongLinkType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ");
            result.ShouldBe("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg");
        }

        [Test]
        public void NullCustomUrl_YoutubeShortLink_ReturnsThumbnail()
        {
            var result = SongThumbnail.Normalize(null, SongLinkType.Youtube, "https://youtu.be/dQw4w9WgXcQ");
            result.ShouldBe("https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg");
        }

        [Test]
        public void NullCustomUrl_YandexLink_ReturnsNull()
        {
            SongThumbnail.Normalize(null, SongLinkType.YandexMusic, "https://music.yandex.ru/album/123")
                .ShouldBeNull();
        }

        [Test]
        public void NullCustomUrl_SoundcloudLink_ReturnsNull()
        {
            SongThumbnail.Normalize(null, SongLinkType.Soundcloud, "https://soundcloud.com/artist/track")
                .ShouldBeNull();
        }

        [Test]
        public void EmptyCustomUrl_YoutubeLink_ReturnsThumbnail()
        {
            var result = SongThumbnail.Normalize("", SongLinkType.Youtube, "https://youtube.com/watch?v=abc123");
            result.ShouldBe("https://img.youtube.com/vi/abc123/hqdefault.jpg");
        }

        [Test]
        public void WhitespaceCustomUrl_YoutubeLink_ReturnsThumbnail()
        {
            var result = SongThumbnail.Normalize("   ", SongLinkType.Youtube, "https://youtube.com/watch?v=abc123");
            result.ShouldBe("https://img.youtube.com/vi/abc123/hqdefault.jpg");
        }

        [Test]
        public void YoutuBeVideoId_ExtractsFromPath()
        {
            var result = SongThumbnail.Normalize(null, SongLinkType.Youtube, "https://youtu.be/abc123xyz");
            result.ShouldBe("https://img.youtube.com/vi/abc123xyz/hqdefault.jpg");
        }

        [Test]
        public void YoutuBeVideoId_WithTimestamp_ExtractsFromPath()
        {
            var result = SongThumbnail.Normalize(null, SongLinkType.Youtube, "https://youtu.be/abc123xyz?t=30");
            result.ShouldBe("https://img.youtube.com/vi/abc123xyz/hqdefault.jpg");
        }

        [Test]
        public void YoutubeUrl_WithMultipleParams_ExtractsVideoId()
        {
            var result = SongThumbnail.Normalize(null,
                SongLinkType.Youtube,
                "https://youtube.com/watch?v=abc123&list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
            result.ShouldBe("https://img.youtube.com/vi/abc123/hqdefault.jpg");
        }

        [Test]
        public void CustomUrlWhitespace_ReturnsCustomUrl()
        {
            SongThumbnail.Normalize("  https://custom.com/thumb.jpg  ",
                    SongLinkType.Youtube,
                    "https://youtube.com/watch?v=abc")
                .ShouldBe("  https://custom.com/thumb.jpg  ");
        }
    }
}
