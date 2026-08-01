using CuMusicClub.Application.TodoItems;
using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.FunctionalTests.TodoItems.Commands;

public class UpdateTodoItemTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoItemId()
    {
        await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoItemService>();

        await Should.ThrowAsync<NotFoundException>(() => service.UpdateAsync(99, "New Title", false));
    }

    [Test]
    public async Task ShouldUpdateTodoItem()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var listService = scope.ServiceProvider.GetRequiredService<ITodoListService>();
        var itemService = scope.ServiceProvider.GetRequiredService<ITodoItemService>();

        var listId = await listService.CreateAsync("New List", null);
        var itemId = await itemService.CreateAsync("New Item", listId);

        await itemService.UpdateAsync(itemId, "Updated Item Title", false);

        var item = await TestApp.FindAsync<TodoItem>(itemId);
        item.ShouldNotBeNull();
        item!.Title.ShouldBe("Updated Item Title");
    }
}
