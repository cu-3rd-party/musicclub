import logging

from aiogram import Router
from aiogram.types import User, CallbackQuery
from aiogram_dialog import Dialog, Window, DialogManager
from aiogram_dialog.widgets.text import Const, Format
from aiogram_dialog.widgets.kbd import Button, Row, Column
from aiogram_dialog.widgets.kbd import ScrollingGroup, Select
from sqlalchemy import select

from bot.models import Song
from bot.services.database import get_db_session
from bot.services.settings import settings
from bot.states.addsong import AddSong
from bot.states.editsong import EditSong
from bot.states.mainmenu import MainMenu
from bot.states.participations import MyParticipations

router = Router()


# ----- Getters -----
async def main_menu_getter(event_from_user: User, **kwargs):
    return {
        "is_admin": event_from_user.id in settings.ADMIN_IDS,
        "chat_link": settings.CHAT_LINK,
    }


async def songs_getter(dialog_manager: DialogManager, **kwargs):
    """Fetch paginated songs for current page."""
    page = dialog_manager.dialog_data.get("page", 0)

    async with get_db_session() as session:
        result = await session.execute(select(Song).order_by(Song.id))
        songs = result.scalars().all()

    total_pages = max((len(songs) - 1) // settings.PAGE_SIZE + 1, 1)
    page %= total_pages
    start = page * settings.PAGE_SIZE
    end = start + settings.PAGE_SIZE
    dialog_manager.dialog_data["total_pages"] = total_pages

    return {
        "songs": songs[start:end],
        "page": page + 1,
        "total_pages": total_pages,
    }


# ----- Button Handlers -----
async def show_song(
    c: CallbackQuery, w: Button, m: DialogManager, item_id: str
):
    await m.start(EditSong.menu, data={"song_id": item_id})


async def next_page(c: CallbackQuery, b: Button, m: DialogManager):
    total_pages = m.dialog_data.get("total_pages", 1)
    page = m.dialog_data.get("page", 0)
    m.dialog_data["page"] = (page + 1) % total_pages
    await m.show()


async def prev_page(c: CallbackQuery, b: Button, m: DialogManager):
    total_pages = m.dialog_data.get("total_pages", 1)
    page = m.dialog_data.get("page", 0)
    m.dialog_data["page"] = (page - 1) % total_pages
    await m.show()


# ----- Dialog Definition -----
router.include_router(
    Dialog(
        # --- Main menu ---
        Window(
            Const("<b>Главное меню</b>\n\nЧто желаешь поделать сегодня?\n"),
            Const("<b>Ты админ, кстати</b>\n", when="is_admin"),
            Button(
                Const("Песни"),
                id="songs",
                on_click=lambda c, b, m: m.switch_to(MainMenu.songs),
            ),
            Button(
                Const("Мои участия"),
                id="participations",
                on_click=lambda c, b, m: m.start(MyParticipations.menu),
            ),
            Button(
                Const("Ближайшие мероприятия"),
                id="concerts",
                on_click=lambda c, b, m: m.switch_to(MainMenu.events),
            ),
            getter=main_menu_getter,
            state=MainMenu.menu,
        ),
        # --- Songs list with pagination ---
        Window(
            Const("<b>Вот список песен</b>\n"),
            Column(
                Select(
                    Format("{item.title}"),
                    id="song_select",
                    item_id_getter=lambda song: song.id,
                    items="songs",
                    on_click=show_song,
                ),
            ),
            Row(
                Button(Const("<"), id="prev", on_click=prev_page),
                Button(
                    Format("{page}/{total_pages}"),
                    id="pagecounter",
                    on_click=lambda c, b, m: c.answer("Мисклик"),
                ),
                Button(Const(">"), id="next", on_click=next_page),
            ),
            Button(
                Const("Добавить песню"),
                id="add_song",
                on_click=lambda c, b, m: m.start(AddSong.title),
            ),
            Button(
                Const("Назад"),
                id="Back",
                on_click=lambda c, b, m: m.switch_to(MainMenu.menu),
            ),
            getter=songs_getter,
            state=MainMenu.songs,
        ),
        # --- Concerts placeholder ---
        Window(
            Const("Ближайшие концерты скоро появятся здесь"),
            Button(
                Const("Назад"),
                id="Back",
                on_click=lambda c, b, m: m.switch_to(MainMenu.menu),
            ),
            state=MainMenu.events,
        ),
    )
)
