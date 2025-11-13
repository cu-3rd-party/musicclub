import logging

from aiogram import Router
from aiogram.enums import ContentType
from aiogram.types import User, CallbackQuery, Message
from aiogram_dialog import Dialog, Window, DialogManager
from aiogram_dialog.widgets.input import MessageInput
from aiogram_dialog.widgets.text import Const, Format
from aiogram_dialog.widgets.kbd import Button, Row, Column, Cancel, Url
from aiogram_dialog.widgets.kbd import ScrollingGroup, Select
from sqlalchemy import select
from sqlalchemy.orm.sync import update
from sqlalchemy.orm import selectinload


from bot.models import Song, SongParticipation, Person
from bot.services.database import get_db_session
from bot.services.role import is_valid_role
from bot.services.settings import settings
from bot.services.url import parse_url
from bot.states.editrole import EditRole

router = Router()


async def main_getter(dialog_manager: DialogManager, **kwargs) -> dict:
    pid = int(dialog_manager.start_data["participation_id"])

    async with get_db_session() as session:
        participation = (
            await session.execute(
                select(SongParticipation)
                .where(SongParticipation.id == pid)
                .options(
                    selectinload(SongParticipation.person),
                    selectinload(SongParticipation.song),
                )
            )
        ).scalar_one_or_none()

    return {
        "participation_id": pid,
        "person_id": participation.person.id,
        "person_name": participation.person.name,
        "song_title": participation.song.title,
        "role": participation.role,
    }


from sqlalchemy import update


async def on_role_input(
    message: Message, msg_input: MessageInput, dialog_manager: DialogManager
):
    new_role = message.text

    if not is_valid_role(new_role):
        return

    participation_id = int(dialog_manager.start_data["participation_id"])

    async with get_db_session() as session:
        await session.execute(
            update(SongParticipation)
            .where(SongParticipation.id == participation_id)
            .values(role=new_role)
        )
        await session.commit()

        participation: SongParticipation = (
            await session.execute(
                select(SongParticipation)
                .where(SongParticipation.id == participation_id)
                .options(selectinload(SongParticipation.song))
            )
        ).scalar_one_or_none()
        await message.bot.send_message(
            chat_id=participation.person_id,
            text=f"{message.from_user.mention_html()} изменил ваше участие в песне <b>{participation.song.title}</b> на <b>{new_role}</b>",
        )

    await dialog_manager.switch_to(EditRole.menu)


router.include_router(
    Dialog(
        Window(
            Format(
                "<b>{person_name}</b>\nв <b>{song_title}</b>\nкак <b>{role}</b>"
            ),
            Url(
                Const("Перейти в профиль"), Format("tg://user?id={person_id}")
            ),
            Button(
                Const("Редактировать название роли"),
                id="edit_role",
                on_click=lambda c, b, m: m.switch_to(EditRole.input_role),
            ),
            Button(
                Const("Удалить"),
                id="remove_role",
                on_click=lambda c, b, m: c.answer("TODO"),
            ),
            Cancel(Const("Назад")),
            getter=main_getter,
            state=EditRole.menu,
        ),
        Window(
            Const("Введи новое название роли"),
            MessageInput(content_types=ContentType.TEXT, func=on_role_input),
            Button(
                Const("Назад"),
                id="back",
                on_click=lambda c, b, m: m.switch_to(EditRole.menu),
            ),
            state=EditRole.input_role,
        ),
    )
)
