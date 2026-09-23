using System.Reflection;
using CuMusicClub.Application.Services.Auth;
using CuMusicClub.Application.Services.Calendar;
using CuMusicClub.Application.Services.DataEntry;
using CuMusicClub.Application.Services.Permission;
using CuMusicClub.Application.Services.Roadie;
using CuMusicClub.Application.Services.Song;
using CuMusicClub.Application.Services.Telegram;
using FluentValidation;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        builder.Services.AddScoped<ISongService, SongService>();
        builder.Services.AddScoped<ITelegramAuthService, TelegramAuthService>();
        builder.Services.AddScoped<ITelegramChatService, TelegramChatService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IDataEntryService, DataEntryService>();
        builder.Services.AddScoped<IRoadieService, RoadieService>();
        builder.Services.AddScoped<ICalendarService, CalendarService>();
        builder.Services.AddScoped<IIcsFeedGenerator, IcsFeedGenerator>();
    }
}