namespace CuMusicClub.Web.Bot;

/// <summary>
/// Тексты бота. Клуб русскоязычный, поэтому бот отвечает по-русски независимо от языка Telegram.
/// </summary>
public static class BotTexts
{
    public const string StartButton = "🎸 Открыть Music Club";

    public const string Welcome =
        "Привет! Это бот музыкального клуба ЦУ 🎸\n\n" +
        "В приложении — песни, роли и твой профиль. Открой его кнопкой ниже.\n" +
        "Список команд — /help";

    public const string StartInvalidParam = "Не понял ссылку. Чтобы открыть приложение, отправьте /start.";

    public const string AuthInvalidToken =
        "Ссылка для входа устарела или уже использована. Начните вход на сайте заново.";

    public const string AuthPrivateOnly = "Подтвердить вход можно только в личных сообщениях с ботом.";

    public const string AuthConfirm =
        "🔐 <b>Вход в Music Club</b>\n\n" +
        "Кто-то хочет войти на сайт под вашим Telegram-аккаунтом. Если это вы — подтвердите.\n\n" +
        "⚠️ Если вы не начинали вход сами, нажмите «Отмена»: иначе другой человек получит доступ к вашему аккаунту.";

    public const string AuthConfirmButton = "✅ Да, это я";
    public const string AuthCancelButton = "✖️ Отмена";
    public const string AuthOk = "✅ Вход подтверждён. Можно вернуться в браузер.";
    public const string AuthCancelled = "Вход отменён.";

    public const string UnknownCommand = "Не знаю такой команды 🤔 Список команд — /help";

    public const string NotRegistered =
        "я пока вас не знаю — откройте приложение через /start в личке с ботом и попробуйте ещё раз.";

    public const string ClubChatOnly = "Эта команда работает только в чате клуба.";

    public const string SongTopicOnly = "Эта команда работает только в топике песни.";

    public const string Cooldown = "⏳ Не так часто — попробуйте через {0} сек.";

    public const string Help =
        """
        📋 <b>Команды бота</b>

        <b>Репетиции</b> — в топике песни:
        /slots — свободные окна на 7 дней
        /slots_with — окна, когда в зале Илья
        <i>…без @user — не учитывать кого-то</i>
        /check 19.02 18:00 — проверить время
        /take 19.02 18:00 — забронировать зал
        /take_with 19.02 18:00 — заявка на репетицию с Ильёй
        /cancel 19.02 18:00 — отменить свою бронь

        <b>Расписание:</b>
        /day [дата] — картинка с расписанием дня
        /status [дата] — брони на день

        <b>Группа и роуди</b> — в топике песни:
        /ping [текст] — позвать всех участников
        /call_my_roadie [текст] — позвать роуди песни
        /ticket_roadie — попросить роуди для группы
        /roadie [текст] — позвать всех роуди

        <b>Организаторам:</b>
        /approve, /reject [дата время] — решить по заявке
        /update — опубликовать сводку на неделю

        Дата — ДД.ММ, день недели (пн…вс), «сегодня» или «завтра». Время — 18, 18:00 или 1800.
        """;
}
