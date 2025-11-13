from aiogram.fsm.state import StatesGroup, State


class EditRole(StatesGroup):
    menu = State()
    remove_confirm = State()
    input_role = State()
