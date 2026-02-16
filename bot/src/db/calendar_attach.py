import logging
from dataclasses import dataclass
from uuid import UUID

logger = logging.getLogger(__name__)

STATE_CALENDAR_URL = 1
STATE_EMAIL_GUESS = 2
STATE_EMAIL_INPUT = 3

_SQL = {
    "get_state": """
        SELECT state, pending_user_id, pending_email
        FROM calendar_attach_state
        WHERE tg_user_id = %s
    """,
    "upsert_state": """
        INSERT INTO calendar_attach_state (tg_user_id, state, pending_user_id, pending_email)
        VALUES (%s, %s, %s, %s)
        ON CONFLICT (tg_user_id) DO UPDATE
        SET state = EXCLUDED.state,
            pending_user_id = EXCLUDED.pending_user_id,
            pending_email = EXCLUDED.pending_email,
            updated_at = NOW()
    """,
    "clear_state": "DELETE FROM calendar_attach_state WHERE tg_user_id = %s",
}


@dataclass(frozen=True)
class CalendarAttachState:
    state: int
    pending_user_id: UUID | None
    pending_email: str | None


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


def _execute_write(
    connection, query: str, params: tuple, *, error_message: str
) -> bool:
    if not _ensure_connection(connection):
        return False
    try:
        with connection and connection.cursor() as cursor:
            cursor.execute(query, params)
            return True
    except Exception as exc:
        logger.error("%s: %s", error_message, exc)
        return False


def get_state(connection, tg_user_id: int) -> CalendarAttachState | None:
    row = _fetch_one(
        connection,
        _SQL["get_state"],
        (tg_user_id,),
        error_message=f"Failed to fetch calendar attach state for tg user {tg_user_id}",
    )
    if not row:
        return None
    state, pending_user_id, pending_email = row
    pending_uuid = UUID(str(pending_user_id)) if pending_user_id else None
    return CalendarAttachState(int(state), pending_uuid, pending_email)


def upsert_state(
    connection,
    tg_user_id: int,
    state: int,
    pending_user_id: UUID | None = None,
    pending_email: str | None = None,
) -> bool:
    return _execute_write(
        connection,
        _SQL["upsert_state"],
        (
            tg_user_id,
            int(state),
            str(pending_user_id) if pending_user_id else None,
            pending_email,
        ),
        error_message=f"Failed to upsert calendar attach state for tg user {tg_user_id}",
    )


def clear_state(connection, tg_user_id: int) -> bool:
    return _execute_write(
        connection,
        _SQL["clear_state"],
        (tg_user_id,),
        error_message=f"Failed to clear calendar attach state for tg user {tg_user_id}",
    )
