using System.Text.Encodings.Web;
using System.Text.Json;
using CuMusicClub.Infrastructure.Data;
using CuMusicClub.Web.Bot;
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
        // Без этого кириллица уходит как \u041A… и в kubectl logs нечитаема
        options.JsonWriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
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

// Логирование и метрики — снаружи обработчика исключений, иначе они видят 200 вместо итогового 4xx/5xx
app.UseRequestLogging();
app.UseHttpMetrics();
app.UseExceptionHandler(options => { });

app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();
app.MapTelegramWebhook();

app.MapFallbackToFile("index.html");

app.Run();
