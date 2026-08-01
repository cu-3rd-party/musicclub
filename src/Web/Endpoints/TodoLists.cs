using CuMusicClub.Application.TodoLists;
using CuMusicClub.Application.TodoLists.Queries.GetTodos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public class TodoLists : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapGet("/", GetTodoLists);
        group.MapPost("/", CreateTodoList);
        group.MapPut("/{id}", UpdateTodoList);
        group.MapDelete("/{id}", DeleteTodoList);
    }

    [EndpointSummary("Get all Todo Lists")]
    public static async Task<Ok<TodosVm>> GetTodoLists(ITodoListService service)
    {
        var vm = await service.GetAllAsync();
        return TypedResults.Ok(vm);
    }

    [EndpointSummary("Create a new Todo List")]
    public static async Task<Created<int>> CreateTodoList(ITodoListService service, CreateTodoListRequest request)
    {
        var id = await service.CreateAsync(request.Title, request.Colour);
        return TypedResults.Created($"/api/TodoLists/{id}", id);
    }

    [EndpointSummary("Update a Todo List")]
    public static async Task<Results<NoContent, BadRequest>> UpdateTodoList(ITodoListService service, int id,
        UpdateTodoListRequest request)
    {
        if (id != request.Id) return TypedResults.BadRequest();

        await service.UpdateAsync(request.Id, request.Title, request.Colour);
        return TypedResults.NoContent();
    }

    [EndpointSummary("Delete a Todo List")]
    public static async Task<NoContent> DeleteTodoList(ITodoListService service, int id)
    {
        await service.DeleteAsync(id);
        return TypedResults.NoContent();
    }
}

public record CreateTodoListRequest(string? Title, string? Colour);

public record UpdateTodoListRequest(int Id, string? Title, string? Colour);
