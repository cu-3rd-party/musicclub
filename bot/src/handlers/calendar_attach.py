import logging
import re
from urllib.parse import urlparse

from aiogram import F, Router
from aiogram.types import (
    CallbackQuery,
    InlineKeyboardButton,
    InlineKeyboardMarkup,
    Message,
)
from aiogram.utils.i18n import gettext as _

import db.calendar
import db.calendar_attach
import db.user
from settings import settings

logger = logging.getLogger(__name__)
router = Router()

_CALLBACK_ATTACH_CALENDAR = "calendar_attach"
_CALLBACK_EMAIL_CONFIRM_YES = "email_confirm_yes"
_CALLBACK_EMAIL_CONFIRM_NO = "email_confirm_no"


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
    return bool(settings.EMAIL_RE.match(value.strip()))


def _normalize_name_part(value: str) -> str:
    return re.sub(r"[^a-zA-Z]", "", value).lower()


def _guess_email_from_name(
    display_name: str | None, fallback_first: str | None, fallback_last: str | None
) -> str | None:
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

    return f"{first_norm[0]}.{last_norm}@{settings.EMAIL_DOMAIN}"


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
        db.calendar_attach.upsert_state(
            settings.db_conn,
            user.id,
            db.calendar_attach.STATE_CALENDAR_URL,
        )
        await query.message.answer(_("calendar.attach.ask"))
        await query.answer()
        return

    guess = _guess_email_from_name(
        profile.display_name,
        user.first_name,
        user.last_name,
    )
    if guess:
        db.calendar_attach.upsert_state(
            settings.db_conn,
            user.id,
            db.calendar_attach.STATE_EMAIL_GUESS,
            pending_user_id=profile.id,
            pending_email=guess,
        )
        await query.message.answer(
            _("email.confirm.prompt").format(email=guess),
            reply_markup=_email_confirm_keyboard(),
        )
        await query.answer()
        return

    db.calendar_attach.upsert_state(
        settings.db_conn,
        user.id,
        db.calendar_attach.STATE_EMAIL_INPUT,
    )
    await query.message.answer(_("email.ask"))
    await query.answer()


@router.message(F.text)
async def calendar_attach_message(message: Message) -> None:
    user = message.from_user
    if not user:
        return

    text = (message.text or "").strip()
    pending = db.calendar_attach.get_state(settings.db_conn, user.id)
    if pending and pending.state == db.calendar_attach.STATE_EMAIL_INPUT:
        if not _is_valid_email(text):
            await message.answer(_("email.invalid"))
            return

        profile = db.user.get_user_by_tg_id(settings.db_conn, user.id)
        if not profile:
            db.calendar_attach.clear_state(settings.db_conn, user.id)
            await message.answer(_("calendar.attach.not_linked"))
            return

        ok = db.user.update_user_email(settings.db_conn, profile.id, text)
        if not ok:
            await message.answer(_("email.save.fail"))
            return

        await message.answer(_("email.save.ok").format(email=text))
        db.calendar_attach.upsert_state(
            settings.db_conn,
            user.id,
            db.calendar_attach.STATE_CALENDAR_URL,
        )
        await message.answer(_("calendar.attach.ask"))
        return

    if not pending or pending.state != db.calendar_attach.STATE_CALENDAR_URL:
        return

    if not _is_valid_ics_url(text):
        await message.answer(_("calendar.attach.invalid_url"))
        return

    user_id = db.calendar.get_user_id_by_tg_id(settings.db_conn, user.id)
    if not user_id:
        db.calendar_attach.clear_state(settings.db_conn, user.id)
        await message.answer(_("calendar.attach.not_linked"))
        return

    ok = db.calendar.upsert_calendar_url(settings.db_conn, user_id, text)
    db.calendar_attach.clear_state(settings.db_conn, user.id)

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

    pending = db.calendar_attach.get_state(settings.db_conn, user.id)
    if not pending or pending.state != db.calendar_attach.STATE_EMAIL_GUESS:
        await query.answer()
        return
    if not pending.pending_user_id or not pending.pending_email:
        db.calendar_attach.clear_state(settings.db_conn, user.id)
        await query.answer()
        return

    ok = db.user.update_user_email(
        settings.db_conn, pending.pending_user_id, pending.pending_email
    )
    if not ok:
        db.calendar_attach.clear_state(settings.db_conn, user.id)
        await query.message.answer(_("email.save.fail"))
        await query.answer()
        return

    await query.message.answer(_("email.save.ok").format(email=pending.pending_email))
    db.calendar_attach.upsert_state(
        settings.db_conn,
        user.id,
        db.calendar_attach.STATE_CALENDAR_URL,
    )
    await query.message.answer(_("calendar.attach.ask"))
    await query.answer()


@router.callback_query(F.data == _CALLBACK_EMAIL_CONFIRM_NO)
async def email_confirm_no(query: CallbackQuery) -> None:
    user = query.from_user
    if not user or not query.message:
        await query.answer()
        return

    pending = db.calendar_attach.get_state(settings.db_conn, user.id)
    if pending and pending.state == db.calendar_attach.STATE_EMAIL_GUESS:
        db.calendar_attach.upsert_state(
            settings.db_conn,
            user.id,
            db.calendar_attach.STATE_EMAIL_INPUT,
        )
        await query.message.answer(_("email.ask"))

    await query.answer()
