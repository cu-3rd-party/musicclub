using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Songs;

public static partial class Songs
{
    [EndpointSummary("Call a roadie for help")]
    private static async Task<Results<Created<RoadieTicketDto>, BadRequest>> CreateTicket(
        IRoadieService service,
        IApplicationUserRepository userRepository,
        ClaimsPrincipal claimsPrincipal,
        Guid songId,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FindByIdAsync(claimsPrincipal.GetUserId()) ??
                   throw new UnauthorizedAccessException();
        var ticket = await service.CreateTicketAsync(songId, user, RoadieTicketType.Help, cancellationToken);
        return TypedResults.Created($"/api/v1/songs/{songId}/roadie-ticket", ticket);
    }

    /// <summary>
    /// Назначает роуди на песню. Доступно только роуди (право <c>roadie.manage</c>).
    /// Тело — { actorUserId }, где актор — целевой роуди (можно назначать себя).
    /// </summary>
    [EndpointSummary("Assign a roadie to a song")]
    private static async Task<Results<Ok<SongDto>, BadRequest<string>>> AssignRoadie(
        IRoadieService roadieService,
        ISongService songService,
        IApplicationUserRepository users,
        ClaimsPrincipal claimsPrincipal,
        Guid songId,
        RoleRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = await users.FindByIdAsync(claimsPrincipal.GetUserId());
        var target = request == null
            ? actor
            : await users.FindByIdAsync(request.ActorUserId);
        if (actor == null || target == null)
            return TypedResults.BadRequest("no target user found");

        await roadieService.AssignRoadieAsync(songId, target, actor, cancellationToken);

        return TypedResults.Ok(await songService.GetAsync(songId, cancellationToken));
    }

    /// <summary>Снимает роуди с песни. Доступно только роуди (право <c>roadie.manage</c>).</summary>
    [EndpointSummary("Remove the roadie from a song")]
    private static async Task<Results<Ok<SongDto>, BadRequest<string>>> RemoveRoadie(
        IRoadieService roadieService,
        ISongService songService,
        IApplicationUserRepository users,
        ClaimsPrincipal claimsPrincipal,
        Guid songId,
        CancellationToken cancellationToken)
    {
        var actor = await users.FindByIdAsync(claimsPrincipal.GetUserId());
        if (actor == null)
            return TypedResults.BadRequest("no target user found");

        await roadieService.RemoveRoadieAsync(songId, actor, cancellationToken);

        return TypedResults.Ok(await songService.GetAsync(songId, cancellationToken));
    }

    /// <summary>Кандидаты на роль роуди — пользователи с правом <c>roadie.manage</c>.</summary>
    [EndpointSummary("Get users that can be assigned as a roadie")]
    private static async Task<Ok<RoleCandidatesDto>> GetRoadieCandidates(
        IRoadieService service,
        string? query,
        CancellationToken cancellationToken)
    {
        var candidates = await service.GetRoadieCandidatesAsync(query, cancellationToken);

        return TypedResults.Ok(new RoleCandidatesDto(candidates
            .Select(u => new SongUserDto(u.Id, u.DisplayName, u.UserName, u.AvatarUrl, u.TgUserId))
            .ToList()));
    }
}
