import logging
from uuid import UUID

from aiogram import Router
from aiogram.filters import CommandStart, CommandObject
from aiogram.types import Message
from aiogram.utils.i18n import gettext as _

from db import execute
from settings import settings

logger = logging.getLogger(__name__)
router = Router()


async def auth_confirm(token: UUID, telegram_user_id: int) -> bool:
    if settings.DB_CONN is None:
        logger.error("Database connection is not available.")
        return False

    try:
        rows = execute(
            settings.DB_CONN,
            "SELECT user_id, success FROM tg_auth_user WHERE id = %s",
            (str(token),),
            fetch=True,
        )
    except Exception as exc:
        logger.error("Failed to fetch auth request: %s", exc)
        return False

    if not rows:
        logger.info("No auth request found for token %s", token)
        return False

    user_id, success = rows[0]
    if success:
        logger.info("Auth token %s already used", token)
        return False

    try:
        execute(
            settings.DB_CONN,
            "UPDATE tg_auth_user SET tg_user_id = %s, success = TRUE WHERE id = %s",
            (telegram_user_id, str(token)),
        )
        execute(
            settings.DB_CONN,
            "UPDATE app_user SET tg_user_id = %s WHERE id = %s",
            (telegram_user_id, str(user_id)),
        )
        execute(
            settings.DB_CONN,
            "UPDATE user_permissions SET edit_own_participation = TRUE, edit_own_songs = TRUE WHERE user_id = %s",
            (str(user_id),),
        )
    except Exception as exc:
        logger.error("Failed to update auth linking for token %s: %s", token, exc)
        return False

    logger.info(
        "Auth confirmed for token %s and telegram user %s", token, telegram_user_id
    )
    return True


@router.message(CommandStart(deep_link=True))
async def cmd_start_with_args(message: Message, command: CommandObject):
    """
    Handles:
      /start auth_<uuid>
    """
    args = command.args
    logger.info("Received command start with %s", args)

    if not args or not args.startswith("auth_"):
        await message.answer(_("start.invalid_param"))
        return

    raw_uuid = args.removeprefix("auth_")

    try:
        token = UUID(raw_uuid)
    except ValueError:
        await message.answer(_("start.invalid_token"))
        return

    ok = await auth_confirm(token, message.from_user.id)

    if ok:
        await message.answer(_("auth.ok"))
    else:
        await message.answer(_("auth.fail"))
