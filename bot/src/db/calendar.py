import logging
from uuid import UUID

logger = logging.getLogger(__name__)

_SQL = {
    "get": "SELECT calendar_url FROM calendar WHERE user_id = %s",
    "create": "INSERT INTO calendar (user_id, calendar_url) VALUES (%s, %s)",
    "update": """
        UPDATE calendar
        SET calendar_url = %s,
            updated_at = NOW()
        WHERE user_id = %s
    """,
    "upsert": """
        INSERT INTO calendar (user_id, calendar_url)
        VALUES (%s, %s)
        ON CONFLICT (user_id) DO UPDATE
        SET calendar_url = EXCLUDED.calendar_url,
            updated_at = NOW()
    """,
    "delete": "DELETE FROM calendar WHERE user_id = %s",
    "list_users_without_calendar": """
        SELECT u.id, u.tg_user_id
        FROM app_user u
        LEFT JOIN calendar c ON c.user_id = u.id
        WHERE c.user_id IS NULL
        ORDER BY u.created_at DESC
    """,
    "get_user_id_by_tg_id": "SELECT id FROM app_user WHERE tg_user_id = %s",
}


def _ensure_connection(connection) -> bool:
    if connection is None:
        logger.error("Database connection is not available.")
        return False
    return True


def _fetch_one(connection, query: str, params: tuple, *, error_message: str):
    if not _ensure_connection(connection):
        return None
    try:
        with connection.cursor() as cursor:
            cursor.execute(query, params)
            return cursor.fetchone()
    except Exception as exc:
        logger.error("%s: %s", error_message, exc)
        return None


def _fetch_all(connection, query: str, params: tuple, *, error_message: str):
    if not _ensure_connection(connection):
        return None
    try:
        with connection.cursor() as cursor:
            cursor.execute(query, params)
            return cursor.fetchall()
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
    if not _ensure_connection(connection):
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


def get_calendar_url(connection, user_id: UUID) -> str | None:
    row = _fetch_one(
        connection,
        _SQL["get"],
        (str(user_id),),
        error_message=f"Failed to fetch calendar url for user {user_id}",
    )
    if not row:
        return None
    return row[0]


def create_calendar_url(connection, user_id: UUID, calendar_url: str) -> bool:
    return _execute_write(
        connection,
        _SQL["create"],
        (str(user_id), calendar_url),
        error_message=f"Failed to create calendar url for user {user_id}",
    )


def update_calendar_url(connection, user_id: UUID, calendar_url: str) -> bool:
    return _execute_write(
        connection,
        _SQL["update"],
        (calendar_url, str(user_id)),
        error_message=f"Failed to update calendar url for user {user_id}",
        expect_rowcount=True,
    )


def upsert_calendar_url(connection, user_id: UUID, calendar_url: str) -> bool:
    return _execute_write(
        connection,
        _SQL["upsert"],
        (str(user_id), calendar_url),
        error_message=f"Failed to upsert calendar url for user {user_id}",
    )


def delete_calendar_url(connection, user_id: UUID) -> bool:
    return _execute_write(
        connection,
        _SQL["delete"],
        (str(user_id),),
        error_message=f"Failed to delete calendar url for user {user_id}",
        expect_rowcount=True,
    )


def list_users_without_calendar(connection) -> list[tuple[UUID, int]] | None:
    rows = _fetch_all(
        connection,
        _SQL["list_users_without_calendar"],
        (),
        error_message="Failed to fetch users without calendar",
    )
    if rows is None:
        return None
    return [(UUID(str(row[0])), int(row[1])) for row in rows]


def get_user_id_by_tg_id(connection, tg_user_id: int) -> UUID | None:
    row = _fetch_one(
        connection,
        _SQL["get_user_id_by_tg_id"],
        (tg_user_id,),
        error_message=f"Failed to fetch user id for tg user {tg_user_id}",
    )
    if not row:
        return None
    return UUID(str(row[0]))
