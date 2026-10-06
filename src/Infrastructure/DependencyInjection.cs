using System.Text;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Data.Interceptors;
using CuMusicClub.Infrastructure.Data.Repositories;
using CuMusicClub.Infrastructure.Yandex;
using CuMusicClub.Infrastructure.YandexCalDav;
using CuMusicClub.Infrastructure.YandexCalDav.Config;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using YandexCalDavDi = CuMusicClub.Infrastructure.YandexCalDav.Config.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
        builder
            .Services.AddOptions<SecurityOptions>()
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                var secret = configuration
                    .GetSection(SecurityOptions.SectionName)
                    .GetValue<string>("Secret");
                if (string.IsNullOrWhiteSpace(secret))
                {
                    if (environment.IsProduction())
                        throw new InvalidOperationException("Security:Secret must be configured in production");

                    secret = SecurityOptions.DefaultJwtKey;
                }

                options.SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            })
            .ValidateOnStart();

        builder
            .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        builder
            .Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<SecurityOptions>>((options, securityOptions) =>
            {
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = securityOptions.Value.SigningKey,

                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,

                    ClockSkew = TimeSpan.Zero,
                };
            });

        builder.Services.AddAuthorizationBuilder();

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.UseNpgsql(connectionString,
                npgOptions => npgOptions.MapEnum<CuMusicClub.Domain.Enums.SongLinkType>());
        });

        // Репозитории принимают базовый DbContext; маппим его на конкретный контекст.
        builder.Services.AddScoped<DbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        builder.Services.AddScoped<ISongRepository, SongRepository>();
        builder.Services.AddScoped<ISongRoleRepository, SongRoleRepository>();
        builder.Services.AddScoped<ISongRoleAssignmentRepository, SongRoleAssignmentRepository>();
        builder.Services.AddScoped<ISongTopicRepository, SongTopicRepository>();
        builder.Services.AddScoped<ITgAuthLinkRepository, TgAuthLinkRepository>();
        builder.Services.AddScoped<IDataEntryRepository, DataEntryRepository>();
        builder.Services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IApplicationUserRepository, ApplicationUserRepository>();
        builder.Services.AddScoped<IRoadieTicketRepository, RoadieTicketRepository>();
        builder.Services.AddScoped<ISongRoadieRepository, SongRoadieRepository>();
        builder.Services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();
        builder.Services.AddScoped<ICalendarFeedRepository, CalendarFeedRepository>();
        builder.Services.AddScoped<IRehearsalBookingRepository, RehearsalBookingRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(options.BotToken);
        });

        builder.Services.AddScoped<IYandexEmailSearchService, YandexEmailSearchService>();

        AddYandexCalendar(builder);
    }

    private static void AddYandexCalendar(IHostApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(YandexWebCalendarOptions.SectionName);
        builder.Services.Configure<YandexWebCalendarOptions>(section);
        var yandexOptions = section.Get<YandexWebCalendarOptions>() ?? new YandexWebCalendarOptions();

        // Расписание зала из ICS-экспорта — не зависит от провайдера и cookies
        builder.Services.AddHttpClient(YandexIcsRoomScheduleProvider.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<IRoomScheduleProvider, YandexIcsRoomScheduleProvider>();

        // Playwright для обновления cookies Яндекс.Календаря
        builder.Services.AddSingleton<IYandexCookieRefresher, YandexCookieRefresher>();

        if (yandexOptions.Provider == YandexCalendarProvider.CalDav)
        {
            // Честный CalDAV под сервисным аккаунтом
            builder.Services.Configure<YandexCaldavConfig>(builder.Configuration.GetSection("YandexCalDav"));
            YandexCalDavDi.AddYandexCalDav(builder.Services);
            builder.Services.AddScoped<ICalDavOperations, CalDavOperationsAdapter>();
            builder.Services.AddScoped<IExternalScheduleProvider, NullExternalScheduleProvider>();
            builder.Services.AddSingleton<ICalendarIntegration, YandexCalDavIntegration>();
            return;
        }

        // Веб-API calendar.yandex.ru от имени пользователя (cookies), cookies обновляет Playwright
        builder.Services
            .AddHttpClient(YandexWebSession.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                client.DefaultRequestHeaders.Add("Origin", "https://calendar.yandex.ru");
                client.DefaultRequestHeaders.Referrer = new Uri("https://calendar.yandex.ru/");
            })
            // Cookie шлём руками из файла, редирект на passport ловим как «сессия протухла»
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            });
        builder.Services.AddSingleton<IYandexWebSession, YandexWebSession>();
        builder.Services.AddSingleton<IYandexMayaClient, YandexMayaClient>();
        builder.Services.AddScoped<ICalDavOperations, YandexWebCalendarOperations>();
        builder.Services.AddScoped<IExternalScheduleProvider, YandexWebScheduleProvider>();
        builder.Services.AddSingleton<ICalendarIntegration, YandexWebCalendarIntegration>();

        // Cookies нужны и без LayerId (занятость, личное расписание); без них сервис сам ничего не делает
        builder.Services.AddHostedService<YandexCookieRefreshHostedService>();
    }
}
