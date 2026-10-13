using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CuMusicClub.Application.Common.Options;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CuMusicClub.Web.Bot;

public static class TelegramWebhook
{
    public const string Path = "/api/telegram/webhook";

    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static bool IsEnabled(this BotOptions options) => !string.IsNullOrWhiteSpace(options.WebhookUrl);

    /// <summary>
    /// secret_token из конфига, иначе HMAC от токена бота: стабилен между рестартами и не требует отдельного секрета.
    /// Telegram допускает 1–256 символов A-Z, a-z, 0-9, _ и -.
    /// </summary>
    public static string ResolveSecret(this BotOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.WebhookSecret)) return options.WebhookSecret.Trim();

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.BotToken.Trim()),
            Encoding.UTF8.GetBytes("musicclub-telegram-webhook"));
        return Convert.ToHexStringLower(hash);
    }

    public static void MapTelegramWebhook(this WebApplication app)
    {
        app.MapPost(Path, HandleAsync).ExcludeFromDescription();
    }

    private static async Task<IResult> HandleAsync(HttpRequest request,
        IOptions<BotOptions> options,
        TelegramUpdateQueue queue,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsEnabled()) return Results.NotFound();

        var expected = Encoding.UTF8.GetBytes(options.Value.ResolveSecret());
        var actual = Encoding.UTF8.GetBytes(request.Headers[SecretHeader].ToString());
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            BotMetrics.WebhookRequest("unauthorized");
            return Results.Unauthorized();
        }

        Update? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(request.Body, JsonBotAPI.Options,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            loggerFactory.CreateLogger(typeof(TelegramWebhook)).LogWarning(ex, "Invalid Telegram webhook payload");
            BotMetrics.WebhookRequest("bad_request");
            return Results.BadRequest();
        }

        if (update is null)
        {
            BotMetrics.WebhookRequest("bad_request");
            return Results.BadRequest();
        }

        if (!queue.TryEnqueue(update))
        {
            BotMetrics.WebhookRequest("queue_full");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        BotMetrics.WebhookRequest("ok");
        return Results.Ok();
    }
}
