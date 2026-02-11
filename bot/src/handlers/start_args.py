import logging
from uuid import UUID

from aiogram import Router
from aiogram.filters import CommandStart, CommandObject
from aiogram.types import Message
from aiogram.utils.i18n import gettext as _

from db.auth import confirm_auth
from settings import settings

logger = logging.getLogger(__name__)
router = Router()


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

    ok = confirm_auth(settings.db_conn, token, message.from_user.id)

    if ok:
        await message.answer(_("auth.ok"))
    else:
        await message.answer(_("auth.fail"))
