using CuMusicClub.Web.Backfill;
using CuMusicClub.Web.Bot;
using Microsoft.AspNetCore.Mvc;

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

        builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));
        builder.Services.AddScoped<BotUpdateHandler>();
        builder.Services.AddHostedService<TelegramBotHostedService>();
        builder.Services.AddHttpClient();
        builder.Services.AddHostedService<ThumbnailBackfillHostedService>();
        builder.Services.AddHostedService<PermissionsBackfillHostedService>();
    }
}
