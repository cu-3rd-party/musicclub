from aiogram.utils.i18n import I18nMiddleware


class MyI18nMiddleware(I18nMiddleware):
    async def get_locale(self, event, data) -> str:
        user = data.get("event_from_user")
        if not user or not user.language_code:
            return "en"
        code = user.language_code.lower()
        if code.startswith("ru"):
            return "ru"
        return "en"
