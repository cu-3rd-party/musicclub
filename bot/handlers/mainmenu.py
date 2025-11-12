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
from bot.states.mainmenu import MainMenu

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
    page_size = 4

    async with get_db_session() as session:
        result = await session.execute(select(Song).order_by(Song.id))
        songs = result.scalars().all()

    total_pages = max((len(songs) - 1) // page_size + 1, 1)
    start = page * page_size
    end = start + page_size

    return {
        "songs": songs[start:end],
        "page": page + 1,
        "total_pages": total_pages,
        "has_prev": page > 0,
        "has_next": page < total_pages - 1,
    }


# ----- Button Handlers -----
async def show_song(c: CallbackQuery, w: Button, m: DialogManager, item_id: str):
    await c.answer(f"Selected song: {item_id}")


async def next_page(c: CallbackQuery, b: Button, m: DialogManager):
    m.dialog_data["page"] = m.dialog_data.get("page", 0) + 1
    await m.show()


async def prev_page(c: CallbackQuery, b: Button, m: DialogManager):
    m.dialog_data["page"] = max(m.dialog_data.get("page", 0) - 1, 0)
    await m.show()


async def add_song(c: CallbackQuery, b: Button, m: DialogManager):
    await c.answer("TODO: add song dialog coming soon 🎶")


# ----- Dialog Definition -----
router.include_router(Dialog(
    # --- Main menu ---
    Window(
        Const("<b>Главное меню</b>\n\nЧто желаешь поделать сегодня?\n"),
        Const("<b>Ты админ, кстати</b>\n", when="is_admin"),
        Button(Const("Песни"), id="songs", on_click=lambda c, b, m: m.switch_to(MainMenu.songs)),
        Button(Const("Ближайшие мероприятия"), id="concerts", on_click=lambda c, b, m: m.switch_to(MainMenu.events)),
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
            Button(Const("<"), id="prev", on_click=prev_page, when="has_prev"),
            Button(Format("{page}/{total_pages}"), id="pagecounter", on_click=lambda c, b, m: c.answer("Мисклик")),
            Button(Const(">"), id="next", on_click=next_page, when="has_next"),
        ),
        Button(Const("Добавить песню"), id="add_song", on_click=add_song),
        Button(Const("Назад"), id="Back", on_click=lambda c, b, m: m.switch_to(MainMenu.menu)),
        getter=songs_getter,
        state=MainMenu.songs,
    ),

    # --- Concerts placeholder ---
    Window(
        Const("Ближайшие концерты скоро появятся здесь"),
        Button(Const("Назад"), id="Back", on_click=lambda c, b, m: m.switch_to(MainMenu.menu)),
        state=MainMenu.events,
    )
))
