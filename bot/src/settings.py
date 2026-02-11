from aiogram.utils.i18n import I18n
from psycopg2._psycopg import connection
from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict

from db import create_connection


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
            path="locales",
            default_locale="en",
            domain="bot",
        )


settings = Settings()
