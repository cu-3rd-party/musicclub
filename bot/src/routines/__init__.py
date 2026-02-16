import asyncio
import logging
from dataclasses import dataclass
from typing import Awaitable, Callable

import fastapi

from .without_calendar import routine as without_calendar_routine

logger = logging.getLogger(__name__)

RoutineFn = Callable[[fastapi.FastAPI], Awaitable[None]]


@dataclass(frozen=True)
class PeriodicRoutine:
    name: str
    interval_seconds: int
    fn: RoutineFn


_PERIODIC_ROUTINES: list[PeriodicRoutine] = [
    PeriodicRoutine("without_calendar", 24 * 60 * 60, without_calendar_routine),
]


async def _run_periodic(app: fastapi.FastAPI, routine: PeriodicRoutine) -> None:
    while True:
        try:
            await routine.fn(app)
        except Exception:
            logger.exception("Routine %s failed", routine.name)
        await asyncio.sleep(routine.interval_seconds)


def on_setup(app: fastapi.FastAPI) -> None:
    tasks = []
    for routine in _PERIODIC_ROUTINES:
        if routine.interval_seconds <= 0:
            logger.warning(
                "Skipping routine %s due to non-positive interval %s",
                routine.name,
                routine.interval_seconds,
            )
            continue
        tasks.append(
            asyncio.create_task(
                _run_periodic(app, routine),
                name=f"routine:{routine.name}",
            )
        )
    app.state.routine_tasks = tasks


def on_shutdown(app: fastapi.FastAPI) -> None:
    tasks = getattr(app.state, "routine_tasks", None)
    if not tasks:
        return
    for task in tasks:
        task.cancel()
