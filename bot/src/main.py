import asyncio
import logging
import os
from aiogram import Bot, Dispatcher

from handlers import router
from middlewares import MyI18nMiddleware
from settings import settings

logger = logging.getLogger(__name__)


async def main():
    logging.basicConfig(
        level=os.environ.get("LOGLEVEL", "INFO").upper(),
        format="%(levelname)s:\t[%(asctime)s] - %(message)s",
    )
    bot = Bot(settings.BOT_TOKEN)
    dp = Dispatcher()

    dp.message.middleware(MyI18nMiddleware(settings.i18n))
    dp.include_router(router)

    logger.info("Starting polling for bot")
    logger.info("WebApp URL: %s", settings.WEBAPP_URL)
    await dp.start_polling(bot)


if __name__ == "__main__":
    asyncio.run(main())
