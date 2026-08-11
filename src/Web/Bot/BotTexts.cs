namespace CuMusicClub.Web.Bot;

public static class BotTexts
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["start.button"] = "🎸 Open Music Club",
        ["start.welcome"] = "Welcome to Music Club! 🎸\n\nTap the button below to open the app:",
        ["help.start"] = "Send /start to get the web app link.",
        ["start.invalid_param"] = "Invalid start parameter.",
        ["start.invalid_token"] = "Invalid or used authentication token.",
        ["auth.ok"] = "✅ Authentication successful! You may return to the web app.",
        ["auth.fail"] = "❌ Authentication failed or expired.",
        ["calendar.attach.ask"] = "Send your calendar ICS URL.",
        ["calendar.attach.invalid_url"] =
            "That does not look like a valid ICS URL. Please send a link ending with .ics.",
        ["calendar.attach.not_linked"] = "Please link your account in the Music Club web app first, then try again.",
        ["calendar.attach.success"] = "✅ Calendar attached.",
        ["calendar.attach.fail"] = "❌ Failed to attach calendar. Please try again later.",
        ["email.confirm.prompt"] = "Is this your email: {0}?",
        ["email.confirm.yes"] = "Yes",
        ["email.confirm.no"] = "No",
        ["email.ask"] = "Please enter your email address.",
        ["email.invalid"] = "That does not look like a valid email address. Please try again.",
        ["email.save.ok"] = "✅ Email saved: {0}",
        ["email.save.fail"] = "❌ Failed to save email. Please try again later.",
    };

    private static readonly IReadOnlyDictionary<string, string> Ru = new Dictionary<string, string>
    {
        ["start.button"] = "🎸 Открыть Music Club",
        ["start.welcome"] = "Добро пожаловать в Music Club! 🎸\n\nНажмите кнопку ниже, чтобы открыть приложение:",
        ["help.start"] = "Отправьте /start, чтобы получить ссылку на веб-приложение.",
        ["start.invalid_param"] = "Некорректный параметр /start.",
        ["start.invalid_token"] = "Неверный токен аутентификации.",
        ["auth.ok"] = "✅ Аутентификация успешна! Можно вернуться в веб-приложение.",
        ["auth.fail"] = "❌ Аутентификация не удалась или истекла.",
        ["calendar.attach.ask"] = "Пришлите ссылку на ваш календарь в формате ICS.",
        ["calendar.attach.invalid_url"] =
            "Похоже, это не ссылка на ICS. Пришлите ссылку, которая заканчивается на .ics.",
        ["calendar.attach.not_linked"] =
            "Сначала привяжите аккаунт в веб‑приложении Music Club, затем попробуйте снова.",
        ["calendar.attach.success"] = "✅ Календарь прикреплён.",
        ["calendar.attach.fail"] = "❌ Не удалось прикрепить календарь. Попробуйте позже.",
        ["email.confirm.prompt"] = "Это ваш email: {0}?",
        ["email.confirm.yes"] = "Да",
        ["email.confirm.no"] = "Нет",
        ["email.ask"] = "Пожалуйста, введите ваш email.",
        ["email.invalid"] = "Похоже, это некорректный email. Попробуйте ещё раз.",
        ["email.save.ok"] = "✅ Email сохранён: {0}",
        ["email.save.fail"] = "❌ Не удалось сохранить email. Попробуйте позже.",
    };

    public static string Get(string key, string? languageCode)
    {
        var isRussian = !string.IsNullOrEmpty(languageCode) &&
                        languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        var table = isRussian ? Ru : En;
        return table.TryGetValue(key, out var text) ? text : key;
    }
}
