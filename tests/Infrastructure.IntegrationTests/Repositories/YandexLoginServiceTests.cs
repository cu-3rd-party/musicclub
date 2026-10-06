using CuMusicClub.Application.Common.Exceptions;
using CuMusicClub.Application.Services.User;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CuMusicClub.Infrastructure.IntegrationTests.Repositories;

public class YandexLoginServiceTests : TestBase
{
    private static async Task<ApplicationUser> CreateUserAsync()
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IApplicationUserRepository>();
        var user = new ApplicationUser
        {
            UserName = $"user-{Guid.NewGuid():N}",
            DisplayName = "Test",
        };
        await users.AddAsync(user);
        await users.SaveChangesAsync();
        return user;
    }

    private static async Task<string?> UpdateAsync(Guid userId, string? login)
    {
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IYandexLoginService>();
        var result = await service.UpdateYandexLoginAsync(userId, login, CancellationToken.None);
        return result.YandexLogin;
    }

    [TestCase("ivan.ivanov", "ivan.ivanov")]
    [TestCase("  Ivan.Ivanov ", "ivan.ivanov")]
    [TestCase("Ivan.Ivanov@edu.centraluniversity.ru", "ivan.ivanov")]
    public async Task Update_NormalizesLogin(string input, string expected)
    {
        var user = await CreateUserAsync();

        (await UpdateAsync(user.Id, input)).ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task Update_EmptyValue_ClearsLogin(string? input)
    {
        var user = await CreateUserAsync();
        await UpdateAsync(user.Id, "someone");

        (await UpdateAsync(user.Id, input)).ShouldBeNull();
    }

    [TestCase("иван")]
    [TestCase("ivan@gmail.com")]
    [TestCase("<script>")]
    public async Task Update_InvalidLogin_Throws(string input)
    {
        var user = await CreateUserAsync();

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync(user.Id, input));
    }

    [Test]
    public async Task Update_LoginTakenByAnotherUser_Throws()
    {
        var first = await CreateUserAsync();
        var second = await CreateUserAsync();
        await UpdateAsync(first.Id, "taken.login");

        await Should.ThrowAsync<ValidationException>(() => UpdateAsync(second.Id, "Taken.Login"));
        (await UpdateAsync(first.Id, "taken.login")).ShouldBe("taken.login");
    }
}
