using CuMusicClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CuMusicClub.Infrastructure.IntegrationTests.Auth;

public partial class AuthServiceTests
{
    [Test]
    public async Task TestUserCreation()
    {
        using var scope = new AuthScope();

        var user = new ApplicationUser
        {
            TgUserId = 1,
            DisplayName = "test user",
            UserName = "testuser",
        };

        var result = await scope.UserManager.CreateAsync(user);

        result.Succeeded.ShouldBeTrue(result.Errors.ToString());

        var returnedUser = await Db()
            .Users.FirstOrDefaultAsync(u => u.TgUserId == user.TgUserId);

        returnedUser.ShouldNotBeNull();
        user.Id.ShouldBe(returnedUser.Id);
        user.TgUserId.ShouldBe(returnedUser.TgUserId);
    }

    [Test]
    public async Task TestUserTelegramUpsertion__Creation()
    {
        using var scope = new AuthScope();

        var tgUser = new Telegram.Bot.Types.User
        {
            Id = 1,
            Username = "testuser",
            FirstName = "test",
            LastName = "user",
            IsBot = false,
        };
        await scope.TelegramAuth.UpsertUserAsync(tgUser, CancellationToken.None);

        var returnedUser = await Db()
            .Users.FirstOrDefaultAsync(u => u.TgUserId == tgUser.Id);

        returnedUser.ShouldNotBeNull();
        tgUser.Id.ShouldBeEquivalentTo(returnedUser.TgUserId);
    }


    [Test]
    public async Task TestUserTelegramUpsertion__Existing()
    {
        using var scope = new AuthScope();

        var user = new ApplicationUser
        {
            TgUserId = 1,
            DisplayName = "test user",
            UserName = "testuser",
        };

        var result = await scope.UserManager.CreateAsync(user);

        result.Succeeded.ShouldBeTrue(result.Errors.ToString());

        // проверяем, что пользователь создался на самом деле
        var createdUser = await Db()
            .Users.FirstOrDefaultAsync(u => u.TgUserId == user.TgUserId);

        createdUser.ShouldNotBeNull();
        user.Id.ShouldBe(createdUser.Id);
        user.TgUserId.ShouldBe(createdUser.TgUserId);

        var tgUser = new Telegram.Bot.Types.User
        {
            Id = 1,
            Username = "testuser",
            FirstName = "test",
            LastName = "user",
            IsBot = false,
        };
        await scope.TelegramAuth.UpsertUserAsync(tgUser, CancellationToken.None);

        var returnedUser = await Db()
            .Users.FirstOrDefaultAsync(u => u.TgUserId == tgUser.Id);

        returnedUser.ShouldNotBeNull();
        tgUser.Id.ShouldBeEquivalentTo(returnedUser.TgUserId);
        user.Id.ShouldBe(returnedUser.Id);
    }
}
