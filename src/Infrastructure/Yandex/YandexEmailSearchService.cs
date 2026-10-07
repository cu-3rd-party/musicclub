using System.Text.Json;
using CuMusicClub.Application.Services.User;
using Microsoft.Extensions.Logging;

namespace CuMusicClub.Infrastructure.Yandex;

/// <summary>
/// Searches Yandex Mail address book (model abook-contacts) for emails by surname/name.
/// Uses the same model as musicscheduler/yandex_api.py::search_email_by_name
/// </summary>
public class YandexEmailSearchService(
    IYandexMailClient mailClient,
    IYandexWebSession session,
    ILogger<YandexEmailSearchService> logger) : IYandexEmailSearchService
{
    private const int FullNamePageSize = 5;
    private const int SurnamePageSize = 20;

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
        var contacts = await SearchContactsAsync(query, FullNamePageSize, ct);
        if (contacts.Length == 1)
            return contacts[0].Email;

        if (contacts.Length > 1)
        {
            var exact = contacts
                .Where(c => Normalize(c.Name) == Normalize(query) || Normalize(c.Name) == Normalize(Swap(query)))
                .ToArray();

            if (exact.Length == 1)
                return exact[0].Email;

            if (exact.Length > 1)
                logger.LogWarning("Поиск '{Query}': {Count} точных совпадений, выбрать нельзя", query, exact.Length);
        }

        // Attempt 2: Search by surname only if we have a given name
        if (string.IsNullOrEmpty(given))
            return null;

        var bySurname = await SearchContactsAsync(surname, SurnamePageSize, ct);
        var s = Normalize(surname);
        var g = Normalize(given);
        var near = bySurname
            .Where(c =>
            {
                var nameParts = Normalize(c.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return nameParts.Contains(s) && nameParts.Any(w => w != s && (w.StartsWith(g) || g.StartsWith(w)));
            })
            .ToArray();

        if (near.Length == 1)
        {
            logger.LogInformation("Поиск '{Query}': нашёл по неточному имени '{Name}'", query, near[0].Name);
            return near[0].Email;
        }

        if (bySurname.Length > 0)
            logger.LogWarning(
                "Поиск '{Query}': среди {Count} однофамильцев имя не совпало",
                query,
                bySurname.Length);

        return null;
    }

    private async Task<Contact[]> SearchContactsAsync(string query, int pageSize, CancellationToken ct)
    {
        var parameters = new { pagesize = pageSize.ToString(), q = query, type = "normal" };
        var data = await mailClient.CallAsync("abook-contacts", parameters, ct);

        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("contact", out var contactsElement)
            || contactsElement.ValueKind != JsonValueKind.Array)
            return [];

        var contacts = new List<Contact>();
        foreach (var contact in contactsElement.EnumerateArray())
        {
            var email = GetEmail(contact);
            if (!string.IsNullOrEmpty(email))
                contacts.Add(new Contact(GetName(contact), email));
        }

        return contacts.ToArray();
    }

    /// <summary>Первый адрес из <c>email: [{ value }]</c>, как в yandex_api.py.</summary>
    private static string? GetEmail(JsonElement contact)
    {
        if (!contact.TryGetProperty("email", out var emails))
            return null;

        if (emails.ValueKind == JsonValueKind.String)
            return emails.GetString();

        if (emails.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var email in emails.EnumerateArray())
        {
            if (email.ValueKind == JsonValueKind.String)
                return email.GetString();
            if (email.ValueKind == JsonValueKind.Object
                && email.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    /// <summary>Имя контакта: строка или объект <c>{ first, last, ... }</c> → «Фамилия Имя».</summary>
    private static string GetName(JsonElement contact)
    {
        if (!contact.TryGetProperty("name", out var name))
            return string.Empty;

        if (name.ValueKind == JsonValueKind.String)
            return name.GetString() ?? string.Empty;

        if (name.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var parts = new[] { "last", "first" }
            .Select(key => name.TryGetProperty(key, out var part) && part.ValueKind == JsonValueKind.String
                ? part.GetString()
                : null)
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var joined = string.Join(" ", parts);
        if (joined.Length > 0)
            return joined;

        return name.TryGetProperty("full", out var full) && full.ValueKind == JsonValueKind.String
            ? full.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string Swap(string query)
    {
        var parts = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? $"{parts[1]} {parts[0]}" : query;
    }

    private static string Normalize(string s)
    {
        return string.Join(" ",
            (s ?? string.Empty)
                .ToLowerInvariant()
                .Replace("ё", "е")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record Contact(string Name, string Email);
}
