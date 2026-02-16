import asyncio
import logging
import os
from contextlib import asynccontextmanager

from aiogram import Bot, Dispatcher, types
from fastapi import FastAPI, Request, HTTPException
from fastapi.responses import Response

import routines
from handlers import router
from middlewares import MyI18nMiddleware
from settings import settings

logger = logging.getLogger(__name__)


def _setup(dp: Dispatcher):
    dp.message.middleware(MyI18nMiddleware(settings.i18n))
    dp.include_router(router)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logging.basicConfig(
        level=os.environ.get("LOGLEVEL", "INFO").upper(),
        format="%(levelname)s:\t[%(asctime)s] - %(message)s",
    )

    bot = Bot(settings.BOT_TOKEN)
    dp = Dispatcher()
    _setup(dp)

    app.state.bot = bot
    app.state.dp = dp

    logger.info("WebApp URL: %s", settings.WEBAPP_URL)

    if settings.WEBHOOK_URL:
        await bot.set_webhook(settings.WEBHOOK_URL, secret_token=settings.secret_token)
        logger.info("Webhook set: %s", settings.WEBHOOK_URL)
    else:
        await bot.delete_webhook()
        logger.info("Webhook deleted (polling mode)")

    routines.on_setup(app)

    try:
        yield
    finally:
        await bot.session.close()
        routines.on_shutdown(app)


app = FastAPI(lifespan=lifespan)


@app.post("/telegram/webhook")
async def handle_webhook(request: Request):
    if request.headers.get("X-Telegram-Bot-Api-Secret-Token") != settings.secret_token:
        raise HTTPException(status_code=403, detail="Forbidden")

    update = types.Update(**await request.json())
    await app.state.dp.feed_webhook_update(app.state.bot, update)
    return Response(status_code=200)


async def main():
    logging.basicConfig(
        level=os.environ.get("LOGLEVEL", "INFO").upper(),
        format="%(levelname)s:\t[%(asctime)s] - %(message)s",
    )

    bot = Bot(settings.BOT_TOKEN)
    dp = Dispatcher()
    _setup(dp)

    logger.info("Starting polling for bot")
    logger.info("WebApp URL: %s", settings.WEBAPP_URL)

    await bot.delete_webhook()
    await dp.start_polling(bot)


if __name__ == "__main__":
    if settings.WEBHOOK_URL:
        raise SystemExit(
            "WEBHOOK_URL is set. Run the webhook server with: uvicorn main:app --host 0.0.0.0 --port 8000"
        )
    asyncio.run(main())
