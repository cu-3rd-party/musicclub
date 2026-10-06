using System.Text.Json;
using CuMusicClub.Application.Services.User;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Searches Yandex Calendar contacts for emails by surname/name.
/// Implements same logic as musicscheduler/yandex_api.py::search_email_by_name
/// </summary>
public class YandexEmailSearchService(
    IYandexMayaClient mayaClient,
    IYandexWebSession session,
    ILogger<YandexEmailSearchService> logger) : IYandexEmailSearchService
{
    public bool IsAvailable
    {
        get { return session.IsConfigured; }
    }

    public async Task<string?> SearchEmailByNameAsync(string query, CancellationToken ct = default)
    {
        query = (query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(query))
            return null;

        // Parse "Фамилия Имя" into parts
        var parts = query.Replace(",", " ").Split()
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (parts.Count == 0)
            return null;

        var surname = parts[0];
        var given = parts.Count > 1 ? parts[1] : null;

        // Attempt 1: Search by full name
        var contacts = await SuggestContactsAsync(query, ct);
        if (contacts is { Length: 1 })
            return contacts[0].GetProperty("email").GetString();

        if (contacts is { Length: > 1 })
        {
            var norm = (string s) => Normalize(s);
            var exact = contacts
                .Where(c => norm(c.GetProperty("name").GetString() ?? "") == norm(query))
                .ToArray();

            if (exact.Length == 1)
                return exact[0].GetProperty("email").GetString();

            if (exact.Length > 1)
                logger.LogWarning("Поиск '{Query}': {Count} точных совпадений, выбрать нельзя", query, exact.Length);
        }

        // Attempt 2: Search by surname only if we have a given name
        if (string.IsNullOrEmpty(given))
            return null;

        var bySurname = await SuggestContactsAsync(surname, ct) ?? [];
        var g = Normalize(given);
        var near = bySurname
            .Where(c =>
            {
                var nameParts = Normalize(c.GetProperty("name").GetString() ?? "").Split();
                return nameParts.Skip(1).Any(w => w.StartsWith(g) || g.StartsWith(w));
            })
            .ToArray();

        if (near.Length == 1)
        {
            logger.LogInformation(
                "Поиск '{Query}': нашёл по неточному имени '{Name}'",
                query,
                near[0].GetProperty("name").GetString());
            return near[0].GetProperty("email").GetString();
        }

        if (bySurname.Length > 0)
            logger.LogWarning(
                "Поиск '{Query}': среди {Count} однофамильцев имя не совпало",
                query,
                bySurname.Length);

        return null;
    }

    private async Task<JsonElement[]?> SuggestContactsAsync(string query, CancellationToken ct)
    {
        var parameters = new { query };
        var result = await mayaClient.CallAsync("suggest-contacts", parameters, ct);

        if (!result.TryGetProperty("contacts", out var contactsElement))
            return null;

        var contacts = new List<JsonElement>();
        foreach (var contact in contactsElement.EnumerateArray())
        {
            if (contact.TryGetProperty("email", out _))
                contacts.Add(contact);
        }

        return contacts.ToArray();
    }

    private static string Normalize(string s)
    {
        return string.Join(" ",
            (s ?? string.Empty)
                .ToLowerInvariant()
                .Replace("ё", "е")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
