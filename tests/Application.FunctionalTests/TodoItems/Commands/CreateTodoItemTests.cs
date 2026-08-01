using CuMusicClub.Application.TodoItems;
using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.FunctionalTests.TodoItems.Commands;

public class CreateTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var listService = scope.ServiceProvider.GetRequiredService<ITodoListService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ITodoItemService>();

        var listId = await listService.CreateAsync("Test List", null);

        var itemId = await itemService.CreateAsync("Test Item", listId);
        itemId.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task ShouldCreateTodoItem()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var listService = scope.ServiceProvider.GetRequiredService<ITodoListService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ITodoItemService>();

        var listId = await listService.CreateAsync("New List", null);

        var itemId = await itemService.CreateAsync("Tasks", listId);

        var item = await TestApp.FindAsync<TodoItem>(itemId);

        item.ShouldNotBeNull();
        item!.ListId.ShouldBe(listId);
        item.Title.ShouldBe("Tasks");
    }
}
