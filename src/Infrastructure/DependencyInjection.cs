using System.Text;
using CuMusicClub.Application.Common.Auth;
using CuMusicClub.Application.Common.Interfaces;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using CuMusicClub.Domain.Entities;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Infrastructure.Data.Interceptors;
using CuMusicClub.Infrastructure.Options;
using CuMusicClub.Infrastructure.Services;
using CuMusicClub.Infrastructure.Services.Auth;
using CuMusicClub.Infrastructure.Services.DataEntry;
using CuMusicClub.Infrastructure.Services.Permission;
using CuMusicClub.Infrastructure.Services.Song;
using CuMusicClub.Infrastructure.Services.Telegram;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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

        builder.Services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        builder.Services.AddScoped<ISongService, SongService>();
        builder.Services.AddScoped<ITelegramAuthService, TelegramAuthService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IDataEntryService, DataEntryService>();

        builder
            .Services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.ClaimsIdentity.UserIdClaimType = "sub";
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
    }
}
