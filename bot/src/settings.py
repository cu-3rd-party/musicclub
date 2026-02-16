import re
import secrets
from pathlib import Path

from aiogram.utils.i18n import I18n
from psycopg2._psycopg import connection
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from db import create_connection

BASE_DIR = Path(__file__).resolve().parent.parent


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=None,
        extra="ignore",
    )

    POSTGRES_USER: str
    POSTGRES_PASSWORD: str
    POSTGRES_DB: str
    POSTGRES_HOST: str
    POSTGRES_PORT: str

    BOT_TOKEN: str
    WEBAPP_URL: str = Field(default="http://localhost:5173")
    SECRET_TOKEN: str | None
    SECRET_TOKEN_LENGTH: int = 24

    EMAIL_DOMAIN: str = "edu.centraluniversity.ru"
    EMAIL_RE: str = r"^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"

    @property
    def webhook_url(self):
        return self.WEBAPP_URL + "/telegram/webhook"

    @property
    def email_re(self) -> re.Pattern[str]:
        return re.compile(self.EMAIL_RE)

    @property
    def secret_token(self) -> str:
        return self.SECRET_TOKEN or secrets.token_urlsafe(self.SECRET_TOKEN_LENGTH)

    @property
    def db_url(self) -> str:
        return (
            f"postgresql://{self.POSTGRES_USER}:{self.POSTGRES_PASSWORD}@"
            f"{self.POSTGRES_HOST}:{self.POSTGRES_PORT}/{self.POSTGRES_DB}?sslmode=disable"
        )

    @property
    def db_conn(self) -> connection:
        return create_connection(self.db_url)

    @property
    def i18n(self) -> I18n:
        return I18n(
            path=str(BASE_DIR / "locales"),
            default_locale="en",
            domain="bot",
        )


settings = Settings()
