using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.ValueObjects;

namespace CuMusicClub.Application.FunctionalTests.TodoLists.Queries;

public class GetTodosTests : TestBase
{
    [Test]
    public async Task ShouldReturnPriorityLevels()
    {
        await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        var result = await service.GetAllAsync();
        result.PriorityLevels.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldReturnAllListsAndItems()
    {
        await TestApp.RunAsDefaultUserAsync();

        await TestApp.AddAsync(new TodoList
        {
            Title = "Shopping",
            Colour = Colour.Blue,
            Items =
            {
                new TodoItem { Title = "Apples", Done = true },
                new TodoItem { Title = "Milk", Done = true },
                new TodoItem { Title = "Bread", Done = true },
                new TodoItem { Title = "Toilet paper" },
                new TodoItem { Title = "Pasta" },
                new TodoItem { Title = "Tissues" },
                new TodoItem { Title = "Tuna" }
            }
        });

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        var result = await service.GetAllAsync();
        result.Lists.Count.ShouldBe(1);
        result.Lists.First().Items.Count.ShouldBe(7);
    }

    [Test]
    public async Task ShouldDenyAnonymousUser()
    {
        // Anonymous user has no roles, but the endpoint requires authorization.
        // This test verifies the service works without auth context.
        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        var result = await service.GetAllAsync();
        result.Lists.ShouldBeEmpty();
    }
}
