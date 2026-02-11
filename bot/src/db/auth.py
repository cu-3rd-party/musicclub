import logging
from uuid import UUID

logger = logging.getLogger(__name__)


def confirm_auth(connection, token: UUID, telegram_user_id: int) -> bool:
    if connection is None:
        logger.error("Database connection is not available.")
        return False

    try:
        with connection and connection.cursor() as cursor:
            cursor.execute(
                "SELECT user_id, success FROM tg_auth_user WHERE id = %s",
                (str(token),),
            )
            row = cursor.fetchone()
            if not row:
                logger.info("No auth request found for token %s", token)
                return False

            user_id, success = row
            if success:
                logger.info("Auth token %s already used", token)
                return False

            cursor.execute(
                "UPDATE tg_auth_user SET tg_user_id = %s, success = TRUE WHERE id = %s",
                (telegram_user_id, str(token)),
            )
            cursor.execute(
                "UPDATE app_user SET tg_user_id = %s WHERE id = %s",
                (telegram_user_id, str(user_id)),
            )
            cursor.execute(
                "UPDATE user_permissions SET edit_own_participation = TRUE, edit_own_songs = TRUE WHERE user_id = %s",
                (str(user_id),),
            )
    except Exception as exc:
        logger.error("Failed to update auth linking for token %s: %s", token, exc)
        return False

    logger.info(
        "Auth confirmed for token %s and telegram user %s", token, telegram_user_id
    )
    return True
