using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Web.Infrastructure.Oidc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CuMusicClub.Web.Endpoints.v1.Auth;

public static partial class Auth
{
    /// <summary>Страница SPA, которая обменивает код входа на сессию.</summary>
    private const string CallbackPage = "/auth/callback";

    [EndpointSummary("Enabled external login providers")]
    private static IReadOnlyList<ExternalProviderDto> Providers(OidcProviderRegistry registry)
    {
        return registry.List();
    }

    [EndpointSummary("Start login with an external provider")]
    private static Results<ChallengeHttpResult, NotFound> ExternalChallenge(OidcProviderRegistry registry,
        string provider,
        string? returnUrl)
    {
        if (!registry.TryGet(provider, out _))
            return TypedResults.NotFound();

        var name = provider.ToLowerInvariant();
        var complete = $"/api/v1/auth/external/{name}/complete?returnUrl={Uri.EscapeDataString(LocalUrl.OrDefault(returnUrl))}";
        return TypedResults.Challenge(new AuthenticationProperties { RedirectUri = complete },
            [OidcProviderRegistry.SchemeOf(name)]);
    }

    /// <summary>
    /// Сюда провайдер возвращает пользователя после callback: находим или заводим пользователя и уводим на SPA
    /// с одноразовым кодом — токены в URL не кладём.
    /// </summary>
    [EndpointSummary("Finish login with an external provider")]
    private static async Task<RedirectHttpResult> ExternalComplete(IExternalLoginService service,
        OidcProviderRegistry registry,
        HttpContext httpContext,
        string provider,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var external = await httpContext.AuthenticateAsync(AuthSchemes.External);
        await httpContext.SignOutAsync(AuthSchemes.External);

        var name = provider.ToLowerInvariant();
        // Временную cookie должна была выпустить именно схема этого провайдера.
        if (!external.Succeeded
            || !registry.TryGet(name, out var options)
            || external.Properties?.Items.TryGetValue(".AuthScheme", out var scheme) != true
            || scheme != OidcProviderRegistry.SchemeOf(name)
            || OidcProviderRegistry.ToIdentity(name, options, external.Principal!) is not { } identity)
            return TypedResults.LocalRedirect("/auth?error=external");

        var user = await service.LoginAsync(identity, cancellationToken);
        var code = service.IssueLoginCode(user.Id);
        return TypedResults.LocalRedirect(
            $"{CallbackPage}?code={Uri.EscapeDataString(code)}&returnUrl={Uri.EscapeDataString(LocalUrl.OrDefault(returnUrl))}");
    }

    [EndpointSummary("Exchange a one-time external login code for an auth session")]
    private static async Task<Results<Ok<AuthSessionDto>, BadRequest>> ExternalExchange(IExternalLoginService service,
        ExternalLoginExchangeRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Code)) return TypedResults.BadRequest();

        var session = await service.RedeemLoginCodeAsync(request.Code, cancellationToken);
        if (session == null) return TypedResults.BadRequest();
        return TypedResults.Ok(session);
    }
}
