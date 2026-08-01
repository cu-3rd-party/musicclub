using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.FunctionalTests.TodoLists.Commands;

public class DeleteTodoListTests : TestBase
{
    [Test]
    public async Task ShouldRequireValidTodoListId()
    {
        await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        await Should.ThrowAsync<NotFoundException>(() => service.DeleteAsync(99));
    }

    [Test]
    public async Task ShouldDeleteTodoList()
    {
        await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        var listId = await service.CreateAsync("New List", null);
        await service.DeleteAsync(listId);

        var list = await TestApp.FindAsync<TodoList>(listId);
        list.ShouldBeNull();
    }
}
