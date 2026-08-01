using CuMusicClub.Application.TodoItems;
using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Domain.Enums;

namespace CuMusicClub.Application.FunctionalTests.TodoItems.Commands;

public class UpdateTodoItemDetailTests : TestBase
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

        await itemService.UpdateDetailAsync(itemId, listId, PriorityLevel.High, "This is the note.");

        var item = await TestApp.FindAsync<TodoItem>(itemId);
        item.ShouldNotBeNull();
        item!.ListId.ShouldBe(listId);
        item.Note.ShouldBe("This is the note.");
        item.Priority.ShouldBe(PriorityLevel.High);
    }
}
