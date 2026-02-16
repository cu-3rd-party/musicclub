import logging
import re
from dataclasses import dataclass
from urllib.parse import urlparse
from uuid import UUID

from aiogram import F, Router
from aiogram.types import CallbackQuery, InlineKeyboardButton, InlineKeyboardMarkup, Message
from aiogram.utils.i18n import gettext as _

import db.calendar
import db.user
from settings import settings

logger = logging.getLogger(__name__)
router = Router()

_CALLBACK_ATTACH_CALENDAR = "calendar_attach"
_CALLBACK_EMAIL_CONFIRM_YES = "email_confirm_yes"
_CALLBACK_EMAIL_CONFIRM_NO = "email_confirm_no"
_EMAIL_DOMAIN = "edu.centraluniversity.ru"
_EMAIL_RE = re.compile(r"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")


@dataclass
class PendingEmailGuess:
    user_id: UUID
    email: str


_PENDING_CALENDAR_USERS: set[int] = set()
_PENDING_EMAIL_GUESSES: dict[int, PendingEmailGuess] = {}
_PENDING_EMAIL_INPUT_USERS: set[int] = set()


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


def _is_valid_email(value: str) -> bool:
    return bool(_EMAIL_RE.match(value.strip()))


def _normalize_name_part(value: str) -> str:
    return re.sub(r"[^a-zA-Z]", "", value).lower()


def _guess_email_from_name(display_name: str | None, fallback_first: str | None, fallback_last: str | None) -> str | None:
    tokens = []
    if display_name:
        tokens = display_name.strip().split()

    if len(tokens) >= 2:
        first = tokens[0]
        last = tokens[-1]
    else:
        first = tokens[0] if tokens else fallback_first
        last = fallback_last

    if not first or not last:
        return None

    first_norm = _normalize_name_part(first)
    last_norm = _normalize_name_part(last)
    if not first_norm or not last_norm:
        return None

    return f"{first_norm[0]}.{last_norm}@{_EMAIL_DOMAIN}"


def _email_confirm_keyboard() -> InlineKeyboardMarkup:
    return InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text=_("email.confirm.yes"),
                    callback_data=_CALLBACK_EMAIL_CONFIRM_YES,
                ),
                InlineKeyboardButton(
                    text=_("email.confirm.no"),
                    callback_data=_CALLBACK_EMAIL_CONFIRM_NO,
                ),
            ]
        ]
    )


@router.callback_query(F.data == _CALLBACK_ATTACH_CALENDAR)
async def calendar_attach_callback(query: CallbackQuery) -> None:
    user = query.from_user
    if not user or not query.message:
        await query.answer()
        return

    profile = db.user.get_user_by_tg_id(settings.db_conn, user.id)
    if not profile:
        await query.message.answer(_("calendar.attach.not_linked"))
        await query.answer()
        return

    if profile.email:
        _PENDING_CALENDAR_USERS.add(user.id)
        await query.message.answer(_("calendar.attach.ask"))
        await query.answer()
        return

    guess = _guess_email_from_name(
        profile.display_name,
        user.first_name,
        user.last_name,
    )
    if guess:
        _PENDING_EMAIL_GUESSES[user.id] = PendingEmailGuess(
            user_id=profile.id,
            email=guess,
        )
        await query.message.answer(
            _("email.confirm.prompt").format(email=guess),
            reply_markup=_email_confirm_keyboard(),
        )
        await query.answer()
        return

    _PENDING_EMAIL_INPUT_USERS.add(user.id)
    await query.message.answer(_("email.ask"))
    await query.answer()


@router.message(F.text)
async def calendar_attach_message(message: Message) -> None:
    user = message.from_user
    if not user:
        return

    text = (message.text or "").strip()
    if user.id in _PENDING_EMAIL_INPUT_USERS:
        if not _is_valid_email(text):
            await message.answer(_("email.invalid"))
            return

        profile = db.user.get_user_by_tg_id(settings.db_conn, user.id)
        if not profile:
            _PENDING_EMAIL_INPUT_USERS.discard(user.id)
            await message.answer(_("calendar.attach.not_linked"))
            return

        ok = db.user.update_user_email(settings.db_conn, profile.id, text)
        if not ok:
            await message.answer(_("email.save.fail"))
            return

        _PENDING_EMAIL_INPUT_USERS.discard(user.id)
        await message.answer(_("email.save.ok").format(email=text))
        _PENDING_CALENDAR_USERS.add(user.id)
        await message.answer(_("calendar.attach.ask"))
        return

    if user.id not in _PENDING_CALENDAR_USERS:
        return

    if not _is_valid_ics_url(text):
        await message.answer(_("calendar.attach.invalid_url"))
        return

    user_id = db.calendar.get_user_id_by_tg_id(settings.db_conn, user.id)
    if not user_id:
        _PENDING_CALENDAR_USERS.discard(user.id)
        await message.answer(_("calendar.attach.not_linked"))
        return

    ok = db.calendar.upsert_calendar_url(settings.db_conn, user_id, text)
    _PENDING_CALENDAR_USERS.discard(user.id)

    if ok:
        await message.answer(_("calendar.attach.success"))
    else:
        await message.answer(_("calendar.attach.fail"))


@router.callback_query(F.data == _CALLBACK_EMAIL_CONFIRM_YES)
async def email_confirm_yes(query: CallbackQuery) -> None:
    user = query.from_user
    if not user or not query.message:
        await query.answer()
        return

    pending = _PENDING_EMAIL_GUESSES.get(user.id)
    if not pending:
        await query.answer()
        return

    ok = db.user.update_user_email(settings.db_conn, pending.user_id, pending.email)
    _PENDING_EMAIL_GUESSES.pop(user.id, None)
    if not ok:
        await query.message.answer(_("email.save.fail"))
        await query.answer()
        return

    await query.message.answer(_("email.save.ok").format(email=pending.email))
    _PENDING_CALENDAR_USERS.add(user.id)
    await query.message.answer(_("calendar.attach.ask"))
    await query.answer()


@router.callback_query(F.data == _CALLBACK_EMAIL_CONFIRM_NO)
async def email_confirm_no(query: CallbackQuery) -> None:
    user = query.from_user
    if not user or not query.message:
        await query.answer()
        return

    if user.id in _PENDING_EMAIL_GUESSES:
        _PENDING_EMAIL_GUESSES.pop(user.id, None)
        _PENDING_EMAIL_INPUT_USERS.add(user.id)
        await query.message.answer(_("email.ask"))

    await query.answer()
