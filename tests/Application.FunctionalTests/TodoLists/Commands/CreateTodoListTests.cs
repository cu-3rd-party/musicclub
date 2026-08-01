using CuMusicClub.Application.TodoLists;
using CuMusicClub.Domain.Entities;

namespace CuMusicClub.Application.FunctionalTests.TodoLists.Commands;

public class CreateTodoListTests : TestBase
{
    [Test]
    public async Task ShouldCreateTodoList()
    {
        var userId = await TestApp.RunAsDefaultUserAsync();

        using var scope = FunctionalTestSetup.ScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITodoListService>();

        var id = await service.CreateAsync("Tasks", null);

        var list = await TestApp.FindAsync<TodoList>(id);
        list.ShouldNotBeNull();
        list!.Title.ShouldBe("Tasks");
    }
}
