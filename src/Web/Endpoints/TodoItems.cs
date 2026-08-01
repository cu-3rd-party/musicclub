using CuMusicClub.Application.TodoItems;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints;

public class TodoItems : IEndpointGroup
{
    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();

        group.MapPost("/", CreateTodoItem);
        group.MapPut("/{id}", UpdateTodoItem);
        group.MapPatch("/UpdateDetail/{id}", UpdateTodoItemDetail);
        group.MapDelete("/{id}", DeleteTodoItem);
    }

    [EndpointSummary("Create a new Todo Item")]
    public static async Task<Created<int>> CreateTodoItem(ITodoItemService service, CreateTodoItemRequest request)
    {
        var id = await service.CreateAsync(request.Title, request.ListId);
        return TypedResults.Created($"/api/TodoItems/{id}", id);
    }

    [EndpointSummary("Update a Todo Item")]
    public static async Task<Results<NoContent, BadRequest>> UpdateTodoItem(ITodoItemService service, int id,
        UpdateTodoItemRequest request)
    {
        if (id != request.Id)
            return TypedResults.BadRequest();

        await service.UpdateAsync(request.Id, request.Title, request.Done);
        return TypedResults.NoContent();
    }

    [EndpointSummary("Update Todo Item Details")]
    public static async Task<Results<NoContent, BadRequest>> UpdateTodoItemDetail(ITodoItemService service, int id,
        UpdateTodoItemDetailRequest request)
    {
        if (id != request.Id) return TypedResults.BadRequest();

        await service.UpdateDetailAsync(request.Id, request.ListId, request.Priority, request.Note);
        return TypedResults.NoContent();
    }

    [EndpointSummary("Delete a Todo Item")]
    public static async Task<NoContent> DeleteTodoItem(ITodoItemService service, int id)
    {
        await service.DeleteAsync(id);
        return TypedResults.NoContent();
    }
}

public record CreateTodoItemRequest(string Title, int ListId);

public record UpdateTodoItemRequest(int Id, string? Title, bool Done);

public record UpdateTodoItemDetailRequest(int Id, int ListId, Domain.Enums.PriorityLevel Priority, string? Note);
