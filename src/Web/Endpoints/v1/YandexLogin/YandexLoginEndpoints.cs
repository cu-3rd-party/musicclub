using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.DTOs.User;
using CuMusicClub.Application.Services.User;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CuMusicClub.Web.Endpoints.v1.YandexLogin;

/// <summary>
/// Эндпоинты для управления YandexLogin пользователей.
/// </summary>
public static class YandexLoginEndpoints
{
    public static void MapYandexLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/")
            .RequireAuthorization()
            .WithTags("YandexLogin")
            .WithOpenApi();

        group.MapGet("/", GetYandexLogin)
            .WithName("GetYandexLogin")
            .WithSummary("Получить YandexLogin текущего пользователя")
            .WithDescription("Возвращает YandexLogin текущего аутентифицированного пользователя");

        group.MapPut("/", UpdateYandexLogin)
            .WithName("UpdateYandexLogin")
            .WithSummary("Обновить YandexLogin")
            .WithDescription("Устанавливает или обновляет YandexLogin текущего пользователя");
    }

    /// <summary>
    /// Получить YandexLogin текущего пользователя.
    /// </summary>
    private static async Task<Results<Ok<YandexLoginDto>, NotFound>> GetYandexLogin(
        ClaimsPrincipal user,
        IYandexLoginService yandexLoginService,
        CancellationToken ct)
    {
        var userId = user.GetUserId();
        var result = await yandexLoginService.GetYandexLoginAsync(userId, ct);

        if (result == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Обновить YandexLogin текущего пользователя.
    /// </summary>
    private static async Task<Results<Ok<YandexLoginDto>, BadRequest<string>, NotFound>> UpdateYandexLogin(
        ClaimsPrincipal user,
        [FromBody] UpdateYandexLoginRequest request,
        IYandexLoginService yandexLoginService,
        CancellationToken ct)
    {
        var userId = user.GetUserId();

        try
        {
            var result = await yandexLoginService.UpdateYandexLoginAsync(userId, request.YandexLogin, ct);
            return TypedResults.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }
}
