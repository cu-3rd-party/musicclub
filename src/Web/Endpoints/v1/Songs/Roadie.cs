using System.Security.Claims;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Services.Roadie;
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
}
