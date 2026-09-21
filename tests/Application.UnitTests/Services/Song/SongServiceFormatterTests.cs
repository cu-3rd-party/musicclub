using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Entities;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Song;

[TestFixture]
[TestOf(typeof(SongServiceFormatter))]
public class SongServiceFormatterTests
{
    [TestFixture]
    public class BuildSongTopicTitleTests
    {
        [Test]
        public void TitleAndArtist_CombinedWithDash()
        {
            SongServiceFormatter
                .BuildSongTopicTitle("Never gonna give you up", "Rick Astley")
                .ShouldBe("Never gonna give you up — Rick Astley");
        }

        [Test]
        public void EmptyArtist_ReturnsTitleOnly()
        {
            SongServiceFormatter
                .BuildSongTopicTitle("Never gonna give you up", "")
                .ShouldBe("Never gonna give you up");
        }

        [Test]
        public void EmptyTitle_ReturnsArtistOnly()
        {
            SongServiceFormatter
                .BuildSongTopicTitle("", "Rick Astley")
                .ShouldBe("Rick Astley");
        }

        [Test]
        public void BothEmpty_ReturnsDefaultSongLabel()
        {
            SongServiceFormatter
                .BuildSongTopicTitle("", "")
                .ShouldBe("Песня");
        }

        [Test]
        public void LongTitle_IsTruncatedToLimit()
        {
            var longTitle = new string('а', 200);
            var result = SongServiceFormatter.BuildSongTopicTitle(longTitle, "Artist");
            result.EnumerateRunes().Count().ShouldBeLessThanOrEqualTo(100);
        }

        [Test]
        public void Html_IsEscaped()
        {
            SongServiceFormatter
                .BuildSongTopicTitle("<b>Title</b>", "Artist")
                .ShouldBe("&lt;b&gt;Title&lt;/b&gt; — Artist");
        }
    }

    [TestFixture]
    public class BuildParticipantMentionTests
    {
        [Test]
        public void WithTgUserId_ReturnsTgLink()
        {
            var user = new ApplicationUser
            {
                DisplayName = "Игорь",
                TgUserId = 774301386,
            };

            SongServiceFormatter
                .BuildParticipantMention(user)
                .ShouldBe("<a href=\"tg://user?id=774301386\">Игорь</a>");
        }

        [Test]
        public void WithoutTgUserId_ReturnsPlainName()
        {
            var user = new ApplicationUser
            {
                DisplayName = "Игорь",
                TgUserId = null,
            };

            SongServiceFormatter
                .BuildParticipantMention(user)
                .ShouldBe("Игорь");
        }

        [Test]
        public void DisplayName_HtmlIsEscaped()
        {
            var user = new ApplicationUser
            {
                DisplayName = "<script>alert(1)</script>",
                TgUserId = 123,
            };

            SongServiceFormatter
                .BuildParticipantMention(user)
                .ShouldBe("<a href=\"tg://user?id=123\">&lt;script&gt;alert(1)&lt;/script&gt;</a>");
        }
    }

    [TestFixture]
    public class BuildParticipantMentionsTests
    {
        [Test]
        public void MultipleParticipants_ReturnsAllMentionsSeparated()
        {
            var participants = new[]
            {
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Игорь", "igamamaev", null, 111),
                    DateTimeOffset.UtcNow),
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Александр", "alex_guitar", null, 222),
                    DateTimeOffset.UtcNow),
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Дмитрий", "drummer_d", null, 333),
                    DateTimeOffset.UtcNow),
            };

            var result = SongServiceFormatter.BuildParticipantMentions(participants);

            result.ShouldBe(
                "<a href=\"tg://user?id=111\">Игорь</a>, " +
                "<a href=\"tg://user?id=222\">Александр</a>, " +
                "<a href=\"tg://user?id=333\">Дмитрий</a>");
        }

        [Test]
        public void SingleParticipant_ReturnsSingleMention()
        {
            var participants = new[]
            {
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Игорь", "igamamaev", null, 111),
                    DateTimeOffset.UtcNow),
            };

            var result = SongServiceFormatter.BuildParticipantMentions(participants);

            result.ShouldBe("<a href=\"tg://user?id=111\">Игорь</a>");
        }

        [Test]
        public void EmptyParticipants_ReturnsEmptyString()
        {
            var result = SongServiceFormatter.BuildParticipantMentions(Array.Empty<RoleAssignmentDto>());

            result.ShouldBeEmpty();
        }

        [Test]
        public void MixedTgUserIds_HandlesNullsCorrectly()
        {
            var participants = new[]
            {
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Игорь", "igamamaev", null, 111),
                    DateTimeOffset.UtcNow),
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Александр", "alex_guitar", null, null),
                    DateTimeOffset.UtcNow),
            };

            var result = SongServiceFormatter.BuildParticipantMentions(participants);

            result.ShouldBe("<a href=\"tg://user?id=111\">Игорь</a>, Александр");
        }

        [Test]
        public void CustomSeparator_UsesProvidedSeparator()
        {
            var participants = new[]
            {
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Игорь", "igamamaev", null, 111),
                    DateTimeOffset.UtcNow),
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Александр", "alex_guitar", null, 222),
                    DateTimeOffset.UtcNow),
            };

            var result = SongServiceFormatter.BuildParticipantMentions(participants, " | ");

            result.ShouldBe("<a href=\"tg://user?id=111\">Игорь</a> | <a href=\"tg://user?id=222\">Александр</a>");
        }
    }

    [TestFixture]
    public class BuildSongFullMessageTests
    {
        [Test]
        public void WithTitleArtistLink_ReturnsComposedMessage()
        {
            var result = SongServiceFormatter.BuildSongFullMessage("Song", "Artist", "https://example.com");
            result.ShouldContain("Песня укомплектована");
            result.ShouldContain("Song — Artist");
            result.ShouldContain("https://example.com");
        }

        [Test]
        public void LinkWithoutProtocol_IsPrefixedWithHttps()
        {
            var result = SongServiceFormatter.BuildSongFullMessage("Song", "Artist", "example.com");
            result.ShouldContain("https://example.com");
        }

        [Test]
        public void LinkWithHttp_IsKept()
        {
            var result = SongServiceFormatter.BuildSongFullMessage("Song", "Artist", "http://example.com");
            result.ShouldContain("http://example.com");
        }
    }

    [TestFixture]
    public class BuildSongCreatedMessageTests
    {
        [Test]
        public void WithCreatedBy_IncludesMention()
        {
            var createdBy = new ApplicationUser
            {
                DisplayName = "Игорь",
                TgUserId = 111,
            };

            var result = SongServiceFormatter.BuildSongCreatedMessage("Song", "Artist", "https://example.com", "https://example.com/song", createdBy);

            result.ShouldContain("Добавлена новая песня");
            result.ShouldContain("tg://user?id=111");
            result.ShouldContain("https://example.com");
        }

        [Test]
        public void WithoutCreatedBy_DoesNotIncludeMention()
        {
            var result = SongServiceFormatter.BuildSongCreatedMessage("Song", "Artist", "https://example.com", "https://example.com/song", null);
            result.ShouldNotContain("Добавил(а)");
        }

        [Test]
        public void EmptyTitleAndArtist_StillReturnsLink()
        {
            var result = SongServiceFormatter.BuildSongCreatedMessage("", "", "https://example.com", "https://example.com/song", null);
            result.ShouldContain("https://example.com");
        }
    }

    [TestFixture]
    public class BuildSongFullTopicMessageTests
    {
        [Test]
        public void WithParticipants_IncludesMentions()
        {
            var participants = new[]
            {
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Вокал", "vocal", null, 111),
                    DateTimeOffset.UtcNow),
                new RoleAssignmentDto(Guid.NewGuid(),
                    new SongUserDto(Guid.NewGuid(), "Гитара", "guitar", null, 222),
                    DateTimeOffset.UtcNow),
            };

            var result = SongServiceFormatter.BuildSongFullTopicMessage("Song", "Artist", "https://example.com", "https://example.com/song", participants);

            result.ShouldContain("Тема для песни готова");
            result.ShouldContain("tg://user?id=111");
            result.ShouldContain("tg://user?id=222");
        }

        [Test]
        public void OnlyLink_ReturnsTopicWithLink()
        {
            var result = SongServiceFormatter.BuildSongFullTopicMessage("", "", "https://example.com", "https://example.com/song", Array.Empty<RoleAssignmentDto>());
            result.ShouldContain("Тема для песни готова");
            result.ShouldContain("https://example.com");
        }
    }
}
