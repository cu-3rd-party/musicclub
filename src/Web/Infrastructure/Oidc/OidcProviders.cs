using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using CuMusicClub.Application.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CuMusicClub.Web.Infrastructure.Oidc;

/// <summary>
/// Один внешний провайдер входа из секции <c>Auth:Oidc:&lt;name&gt;</c>. Новый провайдер (Яндекс ID, Google, …)
/// подключается только конфигом; провайдер с пустым <see cref="ClientId"/> выключен.
/// </summary>
/// <remarks>
/// Два протокола: OIDC с discovery по <see cref="Authority"/> и OAuth 2.0 без discovery (Яндекс ID на
/// oauth.yandex.ru), где профиль читается с <see cref="UserInfoEndpoint"/> и раскладывается в стандартные
/// OIDC-клеймы по <see cref="ClaimKeys"/>.
/// </remarks>
public class OidcProviderOptions
{
    public const string OidcProtocol = "oidc";
    public const string OAuth2Protocol = "oauth2";

    private static readonly string[] DefaultOidcScopes = ["openid", "profile"];

    public string DisplayName { get; set; } = string.Empty;

    /// <summary><c>oidc</c> (по умолчанию) или <c>oauth2</c>.</summary>
    public string Protocol { get; set; } = OidcProtocol;

    /// <summary>OIDC: issuer с <c>/.well-known/openid-configuration</c>.</summary>
    public string? Authority { get; set; }

    /// <summary>OAuth 2.0: адреса авторизации, обмена кода и профиля.</summary>
    public string? AuthorizationEndpoint { get; set; }

    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }

    /// <summary>OAuth 2.0: схема заголовка Authorization к <see cref="UserInfoEndpoint"/>; у Яндекс ID — <c>OAuth</c>.</summary>
    public string UserInfoAuthScheme { get; set; } = "Bearer";

    /// <summary>
    /// OAuth 2.0: стандартный клейм (<c>sub</c>, <c>name</c>, <c>given_name</c>, <c>family_name</c>,
    /// <c>preferred_username</c>, <c>picture</c>) → JSON-ключ ответа <see cref="UserInfoEndpoint"/>.
    /// </summary>
    public Dictionary<string, string> ClaimKeys { get; set; } = new();

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Пусто — для OIDC <c>openid profile</c>.</summary>
    /// <remarks>Без значения по умолчанию: биндер конфигурации дописывает элементы к массиву, а не заменяет его.</remarks>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// OIDC: дозапрашивать профиль с userinfo endpoint из discovery. У Telegram его нет — клеймы только в id_token.
    /// </summary>
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

    /// <summary>
    /// Клейм с числовым id пользователя в Telegram (у Telegram — <c>id</c>, его <c>sub</c> с id не совпадает).
    /// По нему вход находит аккаунт, заведённый через бота или Mini App, и бот узнаёт пользователя.
    /// Пусто — провайдер Telegram ID не отдаёт.
    /// </summary>
    public string? TelegramUserIdClaim { get; set; }

    public bool IsOAuth2
    {
        get { return string.Equals(Protocol, OAuth2Protocol, StringComparison.OrdinalIgnoreCase); }
    }

    public IReadOnlyList<string> EffectiveScopes
    {
        get { return Scopes.Length == 0 && !IsOAuth2 ? DefaultOidcScopes : Scopes; }
    }

    public bool IsEnabled
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ClientId))
                return false;
            if (!IsOAuth2)
                return !string.IsNullOrWhiteSpace(Authority);

            // Без секрета OAuthHandler падает на валидации опций — и вместе с ним каждый запрос.
            return !string.IsNullOrWhiteSpace(ClientSecret)
                   && !string.IsNullOrWhiteSpace(AuthorizationEndpoint)
                   && !string.IsNullOrWhiteSpace(TokenEndpoint)
                   && !string.IsNullOrWhiteSpace(UserInfoEndpoint)
                   && ClaimKeys.ContainsKey("sub");
        }
    }
}

public sealed record ExternalProviderDto(string Name, string DisplayName);

/// <summary>Включённые OIDC-провайдеры; имя провайдера — ключ секции в нижнем регистре.</summary>
public class OidcProviderRegistry
{
    public const string SectionName = "Auth:Oidc";

    private readonly Dictionary<string, OidcProviderOptions> _providers;

    public OidcProviderRegistry(IConfiguration configuration)
    {
        _providers = configuration.GetSection(SectionName).GetChildren()
            .Select(section => (Name: section.Key.ToLowerInvariant(), Options: section.Get<OidcProviderOptions>()))
            .Where(p => p.Options?.IsEnabled == true)
            .ToDictionary(p => p.Name, p => p.Options!);
    }

    public IReadOnlyList<ExternalProviderDto> List()
    {
        return _providers
            .Select(p => new ExternalProviderDto(p.Key, string.IsNullOrEmpty(p.Value.DisplayName) ? p.Key : p.Value.DisplayName))
            .OrderBy(p => p.Name)
            .ToList();
    }

    public bool TryGet(string name, out OidcProviderOptions options)
    {
        return _providers.TryGetValue(name.ToLowerInvariant(), out options!);
    }

    public IEnumerable<KeyValuePair<string, OidcProviderOptions>> All
    {
        get { return _providers; }
    }

    public static string SchemeOf(string provider)
    {
        return AuthSchemes.OidcPrefix + provider.ToLowerInvariant();
    }

    public static string CallbackPathOf(string provider)
    {
        return $"/api/v1/auth/external/{provider.ToLowerInvariant()}/callback";
    }

    /// <summary>Стандартные OIDC-клеймы → провайдер-независимый <see cref="ExternalIdentity"/>.</summary>
    public static ExternalIdentity? ToIdentity(string provider, OidcProviderOptions options, ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        long? telegramUserId = null;
        if (!string.IsNullOrEmpty(options.TelegramUserIdClaim)
            && long.TryParse(principal.FindFirstValue(options.TelegramUserIdClaim), out var tgUserId))
            telegramUserId = tgUserId;

        return new ExternalIdentity(provider,
            subject,
            DisplayNameOf(principal),
            principal.FindFirstValue("preferred_username"),
            principal.FindFirstValue("picture"),
            telegramUserId);
    }

    private static string? DisplayNameOf(ClaimsPrincipal principal)
    {
        var name = principal.FindFirstValue("name");
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        var parts = new[] { principal.FindFirstValue("given_name"), principal.FindFirstValue("family_name") }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(' ', parts);
        return joined.Length == 0 ? null : joined;
    }
}

public static class OidcAuthenticationBuilderExtensions
{
    /// <summary>Регистрирует по схеме на каждый включённый провайдер: OIDC или OAuth 2.0.</summary>
    public static AuthenticationBuilder AddOidcProviders(this AuthenticationBuilder builder,
        OidcProviderRegistry registry)
    {
        builder.Services.AddDistributedMemoryCache();
        builder.Services.TryAddSingleton<ServerSideStateDataFormat>();

        foreach (var (name, provider) in registry.All)
        {
            var scheme = OidcProviderRegistry.SchemeOf(name);
            if (provider.IsOAuth2)
            {
                builder.AddOAuth2Provider(name, provider);
                builder.Services.AddOptions<OAuthOptions>(scheme)
                    .Configure<ServerSideStateDataFormat>((options, state) => options.StateDataFormat = state);
            }
            else
            {
                builder.AddOidcProvider(name, provider);
                builder.Services.AddOptions<OpenIdConnectOptions>(scheme)
                    .Configure<ServerSideStateDataFormat>((options, state) => options.StateDataFormat = state);
            }
        }

        return builder;
    }

    private static void AddOidcProvider(this AuthenticationBuilder builder, string name, OidcProviderOptions provider)
    {
        builder.AddOpenIdConnect(OidcProviderRegistry.SchemeOf(name), provider.DisplayName, options =>
        {
            options.SignInScheme = AuthSchemes.External;
            options.Authority = provider.Authority;
            options.ClientId = provider.ClientId;
            options.ClientSecret = provider.ClientSecret;
            options.CallbackPath = OidcProviderRegistry.CallbackPathOf(name);

            options.ResponseType = OpenIdConnectResponseType.Code;
            // query, а не form_post: callback — обычный GET, и correlation-cookie с SameSite=Lax доходят
            // даже по http в локальной разработке.
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.GetClaimsFromUserInfoEndpoint = provider.GetClaimsFromUserInfoEndpoint;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.Scope.Clear();
            foreach (var scope in provider.EffectiveScopes)
                options.Scope.Add(scope);

            ConfigureCookies(options);
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnRemoteFailure = RedirectToLogin;
        });
    }

    private static void AddOAuth2Provider(this AuthenticationBuilder builder, string name,
        OidcProviderOptions provider)
    {
        builder.AddOAuth(OidcProviderRegistry.SchemeOf(name), provider.DisplayName, options =>
        {
            options.SignInScheme = AuthSchemes.External;
            options.ClientId = provider.ClientId!;
            options.ClientSecret = provider.ClientSecret ?? string.Empty;
            options.CallbackPath = OidcProviderRegistry.CallbackPathOf(name);
            options.AuthorizationEndpoint = provider.AuthorizationEndpoint!;
            options.TokenEndpoint = provider.TokenEndpoint!;
            options.UserInformationEndpoint = provider.UserInfoEndpoint!;
            options.UsePkce = true;
            options.SaveTokens = false;
            options.Scope.Clear();
            foreach (var scope in provider.EffectiveScopes)
                options.Scope.Add(scope);
            foreach (var (claim, jsonKey) in provider.ClaimKeys)
                options.ClaimActions.MapJsonKey(claim, jsonKey);

            ConfigureCookies(options);
            options.Events.OnCreatingTicket = context => LoadUserInfoAsync(context, provider);
            options.Events.OnRemoteFailure = RedirectToLogin;
        });
    }

    /// <summary>Профиль OAuth 2.0-провайдера → клеймы по <see cref="OidcProviderOptions.ClaimKeys"/>.</summary>
    private static async Task LoadUserInfoAsync(OAuthCreatingTicketContext context, OidcProviderOptions provider)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(provider.UserInfoAuthScheme, context.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        context.RunClaimActions(user.RootElement);
    }

    private static void ConfigureCookies(RemoteAuthenticationOptions options)
    {
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }

    private static Task RedirectToLogin(RemoteFailureContext context)
    {
        context.Response.Redirect("/auth?error=external");
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
