using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Web.Middleware;
using Microsoft.Extensions.Logging.Console;
using Prometheus;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
if (builder.Environment.IsProduction())
    // JSON — по строке на событие, чтобы сборщик логов разбирал поля (уровень, категория, scope)
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
else
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });

builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

app.UseForwardedHeaders();

await app.InitialiseDatabaseAsync();

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseCors(static builder => builder
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowAnyOrigin());

app.UseFileServer();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");

app.UseExceptionHandler(options => { });
app.UseHttpMetrics();
app.UseRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
