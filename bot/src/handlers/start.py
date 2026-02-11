import logging

from aiogram import Router
from aiogram.filters import CommandStart
from aiogram.types import (
    Message,
    InlineKeyboardMarkup,
    InlineKeyboardButton,
    WebAppInfo,
)
from aiogram.utils.i18n import gettext as _

from settings import settings

logger = logging.getLogger(__name__)
router = Router()


@router.message(CommandStart())
async def cmd_start(message: Message):
    logger.info("Received command /start without args")

    keyboard = InlineKeyboardMarkup(
        inline_keyboard=[
            [
                InlineKeyboardButton(
                    text=_("start.button"),
                    web_app=WebAppInfo(url=settings.WEBAPP_URL),
                )
            ]
        ]
    )

    await message.answer(
        _("start.welcome"),
        reply_markup=keyboard,
    )
