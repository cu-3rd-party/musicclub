using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
///     Транспорт к внутреннему API веб-интерфейса Яндекс.Почты: POST /web-api/models/liza1?_m={name}.
///     Как search_email_by_name() в musicscheduler/yandex_api.py — ckey и uid передаются в теле запроса.
/// </summary>
public interface IYandexMailClient
{
    /// <summary>
    ///     Вызывает модель и возвращает её <c>data</c>. При ошибке один раз обновляет сессию и повторяет.
    /// </summary>
    Task<JsonElement> CallAsync(string model, object parameters, CancellationToken ct = default);
}

public sealed class YandexMailClient(
    IHttpClientFactory httpClientFactory,
    IYandexWebSession session,
    ILogger<YandexMailClient> logger) : IYandexMailClient
{
    private const string ModelsUrl = "https://mail.yandex.ru/web-api/models/liza1?_m=";

    public async Task<JsonElement> CallAsync(string model, object parameters, CancellationToken ct = default)
    {
        for (var attempt = 0;; attempt++)
            try
            {
                var state = await session.GetStateAsync(ct);
                var ckey = await session.GetMailCKeyAsync(ct);
                return await SendAsync(state, ckey, model, parameters, ct);
            }
            catch (YandexWebAuthException ex) when (attempt == 0)
            {
                logger.LogWarning("♻️ Яндекс.Почта отклонила {Model}: {Reason}. Обновляем сессию...", model, ex.Message);
                await session.RefreshAsync(ct: ct);
            }
            catch (YandexMayaException ex) when (attempt == 0)
            {
                // Как reload_session() в yandex_api.py: любая ошибка модели лечится перечитыванием ckey
                logger.LogWarning("♻️ {Message}. Перечитываем ckey и повторяем...", ex.Message);
                session.Invalidate();
            }
    }

    private async Task<JsonElement> SendAsync(YandexWebAuthState state, string ckey, string model,
        object parameters, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(YandexWebSession.HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, ModelsUrl + model);
        request.Content = JsonContent.Create(new Dictionary<string, object>
        {
            ["models"] = new[]
            {
                new { name = model, @params = parameters, meta = new { requestAttempt = 1 } }
            },
            ["_ckey"] = ckey,
            ["_uid"] = state.Uid
        });
        request.Headers.TryAddWithoutValidation("Cookie", state.CookieHeader);

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
        if (status == "error")
        {
            var error = result.TryGetProperty("error", out var e) ? e.GetRawText() : "unknown";
            throw new YandexMayaException(model, error);
        }

        return result.TryGetProperty("data", out var data) ? data.Clone() : default;
    }
}
