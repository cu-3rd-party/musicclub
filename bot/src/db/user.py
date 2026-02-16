import logging
from dataclasses import dataclass
from uuid import UUID

logger = logging.getLogger(__name__)

_SQL = {
    "get_by_tg_id": "SELECT id, display_name, email FROM app_user WHERE tg_user_id = %s",
    "update_email": """
        UPDATE app_user
        SET email = %s,
            updated_at = NOW()
        WHERE id = %s
    """,
}


@dataclass(frozen=True)
class UserProfile:
    id: UUID
    display_name: str
    email: str | None


def _fetch_one(connection, query: str, params: tuple, *, error_message: str):
    if connection is None:
        logger.error("Database connection is not available.")
        return None
    try:
        with connection.cursor() as cursor:
            cursor.execute(query, params)
            return cursor.fetchone()
    except Exception as exc:
        logger.error("%s: %s", error_message, exc)
        return None


def _execute_write(
    connection,
    query: str,
    params: tuple,
    *,
    error_message: str,
    expect_rowcount: bool = False,
) -> bool:
    if connection is None:
        logger.error("Database connection is not available.")
        return False
    try:
        with connection and connection.cursor() as cursor:
            cursor.execute(query, params)
            if expect_rowcount:
                return cursor.rowcount > 0
            return True
    except Exception as exc:
        logger.error("%s: %s", error_message, exc)
        return False


def get_user_by_tg_id(connection, tg_user_id: int) -> UserProfile | None:
    row = _fetch_one(
        connection,
        _SQL["get_by_tg_id"],
        (tg_user_id,),
        error_message=f"Failed to fetch user for tg user {tg_user_id}",
    )
    if not row:
        return None

    user_id, display_name, email = row
    return UserProfile(UUID(str(user_id)), display_name, email)


def update_user_email(connection, user_id: UUID, email: str) -> bool:
    return _execute_write(
        connection,
        _SQL["update_email"],
        (email, str(user_id)),
        error_message=f"Failed to update email for user {user_id}",
        expect_rowcount=True,
    )
