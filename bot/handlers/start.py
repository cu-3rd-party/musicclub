from aiogram import Router
from aiogram.filters import CommandStart
from aiogram.types import Message
from aiogram_dialog import DialogManager

from bot.models import Person
from bot.services.database import get_db_session

router = Router()


@router.message(CommandStart())
async def start_command(message: Message, dialog_manager: DialogManager) -> None:
    async with get_db_session() as session:
        person = Person(id=message.from_user.id, name=message.from_user.full_name)
        session.add(person)
        await session.commit()
    await message.answer(f"Welcome, to the club, buddy!")
