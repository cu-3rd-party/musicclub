from aiogram import Router

from .calendar_attach import router as calendar_attach_router
from .help import router as help_router
from .start import router as start_router
from .start_args import router as start_args_router

router = Router()
router.include_router(start_args_router)
router.include_router(start_router)
router.include_router(help_router)
router.include_router(calendar_attach_router)
