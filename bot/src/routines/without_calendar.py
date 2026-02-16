import asyncio
import logging

import fastapi
from aiogram.types import InlineKeyboardButton, InlineKeyboardMarkup

import db.calendar
from settings import settings

logger = logging.getLogger(__name__)

_CALLBACK_ATTACH_CALENDAR = "calendar_attach"


def _locale_from_code(code: str | None) -> str:
    if not code:
        return getattr(settings.i18n, "default_locale", "en")
    lowered = code.lower()
    if lowered.startswith("ru"):
        return "ru"
    return "en"


async def routine(app: fastapi.FastAPI) -> None:
    users = db.calendar.list_users_without_calendar(settings.db_conn)
    if not users:
        return

    bot = app.state.bot

    async def _notify_user(tg_id: int) -> None:
        try:
            chat = await bot.get_chat(tg_id)
            locale = _locale_from_code(getattr(chat, "language_code", None))
            text = settings.i18n.gettext("calendar.attach.prompt", locale=locale)
            button_text = settings.i18n.gettext(
                "calendar.attach.button", locale=locale
            )
            keyboard = InlineKeyboardMarkup(
                inline_keyboard=[
                    [
                        InlineKeyboardButton(
                            text=button_text, callback_data=_CALLBACK_ATTACH_CALENDAR
                        )
                    ]
                ]
            )
            await bot.send_message(tg_id, text, reply_markup=keyboard)
        except Exception as exc:
            logger.warning("Failed to notify user without calendar: %s", exc)

    tasks = [
        _notify_user(tg_id)
        for _, tg_id in users
        if tg_id
    ]

    if tasks:
        await asyncio.gather(*tasks)
