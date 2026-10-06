using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Транспорт к внутреннему API веб-интерфейса Яндекс.Календаря (Maya): POST /api/models?_models={name}.
///     Это не публичное API — мы делаем вид, что мы браузер пользователя (см. musicscheduler/yandex_api.py).
/// </summary>
public interface IYandexMayaClient
{
    /// <summary>
    ///     Вызывает модель и возвращает её <c>data</c>. При ошибке авторизации один раз обновляет сессию и повторяет.
    /// </summary>
    Task<JsonElement> CallAsync(string model, object parameters, CancellationToken ct = default);
}

public sealed class YandexMayaClient(
    IHttpClientFactory httpClientFactory,
    IYandexWebSession session,
    IOptions<YandexWebCalendarOptions> options,
    ILogger<YandexMayaClient> logger) : IYandexMayaClient
{
    private const string ModelsUrl = "https://calendar.yandex.ru/api/models?_models=";

    public async Task<JsonElement> CallAsync(string model, object parameters, CancellationToken ct = default)
    {
        for (var attempt = 0;; attempt++)
            try
            {
                var state = await session.GetStateAsync(ct);
                return await SendAsync(state, model, parameters, ct);
            }
            catch (YandexWebAuthException ex) when (attempt == 0)
            {
                logger.LogWarning("♻️ Яндекс отклонил {Model}: {Reason}. Обновляем сессию...", model, ex.Message);
                await session.RefreshAsync(ct: ct);
            }
            catch (YandexMayaException ex) when (attempt == 0)
            {
                // Как reload_session() в yandex_api.py: ckey мог протухнуть с невнятной ошибкой — перечитываем его
                logger.LogWarning("♻️ {Message}. Перечитываем ckey и повторяем...", ex.Message);
                session.Invalidate();
            }
    }

    private async Task<JsonElement> SendAsync(YandexWebAuthState state, string model, object parameters,
        CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(YandexWebSession.HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, ModelsUrl + model);
        request.Content = JsonContent.Create(new
        {
            models = new[] { new { name = model, @params = parameters } }
        });
        request.Headers.TryAddWithoutValidation("Cookie", state.CookieHeader);
        request.Headers.TryAddWithoutValidation("x-yandex-maya-ckey", state.CKey);
        request.Headers.TryAddWithoutValidation("x-yandex-maya-uid", state.Uid);
        request.Headers.TryAddWithoutValidation("x-yandex-maya-cid",
            $"MAYA-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        request.Headers.TryAddWithoutValidation("x-yandex-maya-timezone", options.Value.TimeZone);

        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || (int)response.StatusCode is >= 300 and < 400)
            throw new YandexWebAuthException($"HTTP {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync(ct);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            // Вместо JSON пришла HTML-страница (капча/логин) — сессия невалидна
            throw new YandexWebAuthException($"Ответ не JSON (HTTP {(int)response.StatusCode})");
        }

        using var _ = doc;
        var root = doc.RootElement;

        if (!root.TryGetProperty("models", out var models) || models.GetArrayLength() == 0)
            throw new YandexWebAuthException("В ответе нет models");

        var result = models[0];
        var status = result.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status == "ok")
            return result.TryGetProperty("data", out var data) ? data.Clone() : default;

        var error = result.TryGetProperty("error", out var e) ? e.GetRawText() : "unknown";

        // ckey живёт недолго; в yandex_api.py любая ошибка модели лечилась reload_session()
        if (IsAuthError(error))
            throw new YandexWebAuthException(error);

        throw new YandexMayaException(model, error);
    }

    private static bool IsAuthError(string error)
    {
        return error.Contains("ckey", StringComparison.OrdinalIgnoreCase)
               || error.Contains("auth", StringComparison.OrdinalIgnoreCase)
               || error.Contains("session", StringComparison.OrdinalIgnoreCase)
               || error.Contains("uid", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
///     Модель Maya вернула бизнес-ошибку (не связанную с авторизацией).
/// </summary>
public class YandexMayaException(string model, string error)
    : Exception($"Яндекс.Календарь: ошибка модели {model}: {error}")
{
    public string Model { get; } = model;
    public string Error { get; } = error;
}
