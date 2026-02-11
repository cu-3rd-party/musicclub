import logging

import psycopg2
from psycopg2 import OperationalError

logger = logging.getLogger(__name__)


def create_connection(url: str):
    connection = None
    try:
        connection = psycopg2.connect(url)
    except OperationalError as e:
        logger.error("%s", e)
    return connection


def _is_query_safe(query: str) -> bool:
    """
    Light-weight SQL injection guard.
    Blocks stacked statements and obvious comment-based injections.
    """
    stripped = query.strip()
    # Disallow multiple statements separated by semicolons.
    if ";" in stripped[:-1]:
        return False
    lowered = stripped.lower()
    # Basic checks for attempts to short-circuit predicates or hide payloads.
    blocked_tokens = (";--", "--", "/*", "*/", " or 1=1", " or '1'='1'")
    return not any(token in lowered for token in blocked_tokens)


def execute(connection, query: str, args=None, fetch: bool = False):
    """
    Execute a query with optional parameters.

    Args:
        connection: psycopg2 connection.
        query: SQL query with placeholders (%s).
        args: sequence or mapping passed to cursor.execute for parameter binding.
        fetch: when True, returns cursor.fetchall(); otherwise returns rowcount.
    """
    if connection is None:
        raise ValueError("Connection is not initialized")

    if not _is_query_safe(query):
        raise ValueError("Potential SQL injection detected in query")

    with connection.cursor() as cursor:
        try:
            cursor.execute(query, args)
            if fetch:
                return cursor.fetchall()
            connection.commit()
            return cursor.rowcount
        except Exception as exc:
            connection.rollback()
            logger.error("Failed to execute query: %s", exc)
            raise
