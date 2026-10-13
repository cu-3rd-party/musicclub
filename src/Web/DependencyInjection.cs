using CuMusicClub.Application.Common.Options;
using CuMusicClub.Web.Backfill;
using CuMusicClub.Web.Bot;
using CuMusicClub.Web.Infrastructure.Oidc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddOpenApi();

        builder.Services.AddCors();

        builder.Services.AddHealthChecks();

        // /metrics на отдельном порту, чтобы не светить их через ingress; 0 — выключено (тесты)
        var metricsPort = builder.Configuration.GetValue("Metrics:Port", 8082);
        if (metricsPort > 0)
            builder.Services.AddMetricServer(options => options.Port = (ushort) metricsPort);

        // Бэкенд доступен только через прокси (traefik): доверяем X-Forwarded-*, иначе redirect_uri для OIDC
        // соберётся с http:// вместо https://.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.AddExternalAuth();

        builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));
        builder.Services.AddScoped<BotUpdateHandler>();
        builder.Services.AddScoped<BotRehearsalCommandsHandler>();
        builder.Services.AddScoped<BotScheduleCommandsHandler>();
        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddHttpClient();
        builder.Services.AddHostedService<ThumbnailBackfillHostedService>();
        builder.Services.AddHostedService<PermissionsBackfillHostedService>();
        builder.Services.AddHostedService<RoadieBackfillHostedService>();
        builder.Services.AddHostedService<RoadieAutoAssignHostedService>();
        builder.Services.AddHostedService<TopicBackfillHostedService>();
        builder.Services.AddHostedService<CalendarSyncBackfillHostedService>();
        builder.Services.AddHostedService<YandexLoginBackfillHostedService>();
    }

    /// <summary>
    /// Вход через внешние провайдеры (OIDC / OAuth 2.0) из секции <c>Auth:Oidc</c>. Основная схема — JWT
    /// (см. Infrastructure); cookie <see cref="AuthSchemes.External"/> живёт только между callback провайдера
    /// и выдачей кода входа.
    /// </summary>
    private static void AddExternalAuth(this IHostApplicationBuilder builder)
    {
        var oidcProviders = new OidcProviderRegistry(builder.Configuration);
        builder.Services.AddSingleton(oidcProviders);

        builder.Services
            .AddAuthentication()
            .AddCookie(AuthSchemes.External, options =>
            {
                options.Cookie.Name = "mc_external";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddOidcProviders(oidcProviders);
    }
}
