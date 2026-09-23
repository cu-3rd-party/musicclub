using System.Text;
using CuMusicClub.Application.Common.Options;
using CuMusicClub.Domain.Abstractions;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Data.Interceptors;
using CuMusicClub.Infrastructure.Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;

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
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(options.BotToken);
        });
    }
}
