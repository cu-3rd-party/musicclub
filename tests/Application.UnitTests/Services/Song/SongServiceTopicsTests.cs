using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Entities;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.Song;

[TestFixture]
[TestOf(typeof(SongServiceTopics))]
public class SongServiceTopicsTests
{
    private const long TopicId = 42;

    private readonly ApplicationUser _member = new() { Id = Guid.NewGuid(), TgUserId = 100, DisplayName = "Петя" };
    private readonly ApplicationUser _admin = new() { Id = Guid.NewGuid(), TgUserId = 200, DisplayName = "Аня <админ>" };

    private Mock<ITelegramChatService> _telegram = null!;
    private SongServiceTopics _topics = null!;
    private string? _sent;

    [SetUp]
    public void SetUp()
    {
        _sent = null;
        _telegram = new Mock<ITelegramChatService>();
        _telegram.Setup(t => t.SendTopicMessage(TopicId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<long, string, CancellationToken>((_, text, _) => _sent = text);
        _topics = new SongServiceTopics(_telegram.Object);
    }

    [Test]
    public async Task Leave_BySomeoneElse_SaysWhoRemovedWhom()
    {
        await _topics.AnnounceParticipantLeaveAsync(TopicId, _member, "Бас", _admin);

        _sent.ShouldBe("Аня &lt;админ&gt; убрал(а) <a href=\"tg://user?id=100\">Петя</a> с роли Бас");
    }

    [Test]
    public async Task Join_BySomeoneElse_SaysWhoAddedWhom()
    {
        await _topics.AnnounceParticipantJoinAsync(TopicId, _member, "Бас", _admin);

        _sent.ShouldBe("Аня &lt;админ&gt; добавил(а) <a href=\"tg://user?id=100\">Петя</a> в песню как Бас");
    }

    [Test]
    public async Task Self_KeepsShortMessage()
    {
        await _topics.AnnounceParticipantLeaveAsync(TopicId, _member, "Бас", _member);
        _sent.ShouldBe("<a href=\"tg://user?id=100\">Петя</a> покинул песню как Бас");

        await _topics.AnnounceParticipantJoinAsync(TopicId, _member, "Бас", _member);
        _sent.ShouldBe("<a href=\"tg://user?id=100\">Петя</a> присоединился к песне как Бас");
    }
}
