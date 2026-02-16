import logging
from urllib.parse import urlparse

from aiogram import F, Router
from aiogram.types import CallbackQuery, Message
from aiogram.utils.i18n import gettext as _

import db.calendar
from settings import settings

logger = logging.getLogger(__name__)
router = Router()

_CALLBACK_ATTACH_CALENDAR = "calendar_attach"
_PENDING_USERS: set[int] = set()


def _is_valid_ics_url(value: str) -> bool:
    try:
        parsed = urlparse(value.strip())
    except ValueError:
        return False
    if parsed.scheme not in ("http", "https"):
        return False
    if not parsed.netloc:
        return False
    path = parsed.path.lower()
    return ".ics" in path


@router.callback_query(F.data == _CALLBACK_ATTACH_CALENDAR)
async def calendar_attach_callback(query: CallbackQuery) -> None:
    user = query.from_user
    if not user or not query.message:
        await query.answer()
        return

    _PENDING_USERS.add(user.id)
    await query.message.answer(_("calendar.attach.ask"))
    await query.answer()


@router.message(F.text)
async def calendar_attach_message(message: Message) -> None:
    user = message.from_user
    if not user or user.id not in _PENDING_USERS:
        return

    text = (message.text or "").strip()
    if not _is_valid_ics_url(text):
        await message.answer(_("calendar.attach.invalid_url"))
        return

    user_id = db.calendar.get_user_id_by_tg_id(settings.db_conn, user.id)
    if not user_id:
        _PENDING_USERS.discard(user.id)
        await message.answer(_("calendar.attach.not_linked"))
        return

    ok = db.calendar.upsert_calendar_url(settings.db_conn, user_id, text)
    _PENDING_USERS.discard(user.id)

    if ok:
        await message.answer(_("calendar.attach.success"))
    else:
        await message.answer(_("calendar.attach.fail"))
