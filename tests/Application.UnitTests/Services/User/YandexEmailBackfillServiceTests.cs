using CuMusicClub.Application.Services.User;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace CuMusicClub.Application.UnitTests.Services.User;

[TestFixture]
[TestOf(typeof(YandexEmailBackfillService))]
public class YandexEmailBackfillServiceTests
{
    private Mock<IApplicationUserRepository> _users = null!;
    private Mock<IYandexLoginGuessRepository> _guesses = null!;
    private Mock<IYandexEmailSearchService> _search = null!;
    private Mock<IUnitOfWork> _unitOfWork = null!;
    private YandexEmailBackfillService _service = null!;
    private YandexLoginGuess? _added;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IApplicationUserRepository>();
        _guesses = new Mock<IYandexLoginGuessRepository>();
        _search = new Mock<IYandexEmailSearchService>();
        _unitOfWork = new Mock<IUnitOfWork>();
        _added = null;

        _guesses
            .Setup(g => g.AddAsync(It.IsAny<YandexLoginGuess>(), It.IsAny<CancellationToken>()))
            .Callback<YandexLoginGuess, CancellationToken>((g, _) => _added = g)
            .Returns(Task.CompletedTask);

        _service = new YandexEmailBackfillService(_users.Object,
            _guesses.Object,
            _search.Object,
            _unitOfWork.Object,
            NullLogger<YandexEmailBackfillService>.Instance);
    }

    private static ApplicationUser User(string displayName)
    {
        return new ApplicationUser { Id = Guid.NewGuid(), DisplayName = displayName };
    }

    [Test]
    public async Task GuessLoginAsync_UniqueCorporateEmail_SetsLoginAndRecordsFound()
    {
        var user = User("Иванов Иван");
        _search
            .Setup(s => s.SearchEmailByNameAsync("Иванов Иван", It.IsAny<CancellationToken>()))
            .ReturnsAsync("I.Ivanov@edu.centraluniversity.ru");

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Found);
        user.YandexLogin.ShouldBe("i.ivanov");
        _added.ShouldNotBeNull();
        _added.UserId.ShouldBe(user.Id);
        _added.Query.ShouldBe("Иванов Иван");
        _added.Email.ShouldBe("I.Ivanov@edu.centraluniversity.ru");
        _added.Status.ShouldBe(YandexLoginGuessStatus.Found);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GuessLoginAsync_TriesSwappedNameOrder()
    {
        var user = User("Иван Иванов");
        _search
            .Setup(s => s.SearchEmailByNameAsync("Иванов Иван", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ivanov@edu.centraluniversity.ru");

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Found);
        user.YandexLogin.ShouldBe("ivanov");
    }

    [Test]
    public async Task GuessLoginAsync_FirstAndLastNameSet_SearchesByThemInsteadOfDisplayName()
    {
        var user = User("vanya_rock");
        user.FirstName = " Иван ";
        user.LastName = "Иванов";
        _search
            .Setup(s => s.SearchEmailByNameAsync("Иванов Иван", It.IsAny<CancellationToken>()))
            .ReturnsAsync("ivanov@edu.centraluniversity.ru");

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Found);
        user.YandexLogin.ShouldBe("ivanov");
        _added!.Query.ShouldBe("Иванов Иван");
        _search.Verify(s => s.SearchEmailByNameAsync("vanya_rock", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GuessLoginAsync_FirstAndLastNameSet_TriesNameSurnameOrder()
    {
        var user = User("vanya_rock");
        user.FirstName = "Анна";
        user.LastName = "Мария Петрова";
        _search
            .Setup(s => s.SearchEmailByNameAsync("Анна Мария Петрова", It.IsAny<CancellationToken>()))
            .ReturnsAsync("petrova@edu.centraluniversity.ru");

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Found);
        user.YandexLogin.ShouldBe("petrova");
    }

    [Test]
    public async Task GuessLoginAsync_NothingFound_RecordsNotFound()
    {
        var user = User("Иван Иванов");

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.NotFound);
        user.YandexLogin.ShouldBeNull();
        _added!.Status.ShouldBe(YandexLoginGuessStatus.NotFound);
    }

    [Test]
    public async Task GuessLoginAsync_LoginOwnedByAnotherUser_RecordsConflict()
    {
        var user = User("Иванов Иван");
        _search
            .Setup(s => s.SearchEmailByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ivanov@edu.centraluniversity.ru");
        _users
            .Setup(u => u.FindByYandexLoginAsync("ivanov", It.IsAny<CancellationToken>()))
            .ReturnsAsync(User("Другой"));

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Conflict);
        user.YandexLogin.ShouldBeNull();
        _added!.Email.ShouldBe("ivanov@edu.centraluniversity.ru");
    }

    [Test]
    public async Task GuessLoginAsync_SearchThrows_RecordsErrorAndUpdatesExistingRow()
    {
        var user = User("Иванов Иван");
        var existing = new YandexLoginGuess { UserId = user.Id, Status = YandexLoginGuessStatus.Error };
        _guesses
            .Setup(g => g.FindByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _search
            .Setup(s => s.SearchEmailByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        var status = await _service.GuessLoginAsync(user);

        status.ShouldBe(YandexLoginGuessStatus.Error);
        _added.ShouldBeNull();
        existing.Error.ShouldBe("boom");
        existing.AttemptedAt.ShouldNotBe(default);
    }

    [Test]
    public async Task BackfillAllUsersAsync_StopsAfterConsecutiveErrors()
    {
        var users = Enumerable.Range(0, 5).Select(i => User($"User {i}")).ToList();
        _guesses
            .Setup(g => g.GetUsersToGuessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        _search
            .Setup(s => s.SearchEmailByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("cookies expired"));

        var result = await _service.BackfillAllUsersAsync();

        result.Errors.ShouldBe(3);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
