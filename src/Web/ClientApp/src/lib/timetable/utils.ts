import type { PositionedTimetableEvent, TimetableEvent } from "./types";

const MINUTES_IN_DAY = 24 * 60;

export function startOfDay(date: Date): Date {
    const result = new Date(date);
    result.setHours(0, 0, 0, 0);
    return result;
}

export function addDays(date: Date, days: number): Date {
    const result = new Date(date);
    result.setDate(result.getDate() + days);
    return result;
}

/** Понедельник недели, в которую попадает дата. */
export function startOfWeek(date: Date): Date {
    const result = startOfDay(date);
    const shift = (result.getDay() + 6) % 7;
    return addDays(result, -shift);
}

export function isSameDay(a: Date, b: Date): boolean {
    return (
        a.getFullYear() === b.getFullYear() &&
        a.getMonth() === b.getMonth() &&
        a.getDate() === b.getDate()
    );
}

/**
 * Кусок события, попадающий в конкретный день (события через полночь режутся по дням).
 */
export function eventsForDay(
    events: TimetableEvent[],
    day: Date,
): TimetableEvent[] {
    const dayStart = startOfDay(day).getTime();
    const dayEnd = addDays(startOfDay(day), 1).getTime();

    return events
        .filter(
            (e) => e.startAt.getTime() < dayEnd && e.endAt.getTime() > dayStart,
        )
        .map((e) => ({
            ...e,
            start: Math.max(0, (e.startAt.getTime() - dayStart) / 60_000),
            end: Math.min(
                MINUTES_IN_DAY,
                (e.endAt.getTime() - dayStart) / 60_000,
            ),
        }));
}

/**
 * Раскладывает пересекающиеся события по дорожкам (как в Яндекс/Google Календаре).
 */
export function layoutEvents(
    events: TimetableEvent[],
): PositionedTimetableEvent[] {
    const sorted = [...events].sort(
        (a, b) => a.start - b.start || b.end - a.end,
    );
    const result: PositionedTimetableEvent[] = [];

    let cluster: PositionedTimetableEvent[] = [];
    let laneEnds: number[] = [];
    let clusterEnd = -1;

    const flush = () => {
        for (const e of cluster) e.lanes = laneEnds.length;
        result.push(...cluster);
        cluster = [];
        laneEnds = [];
    };

    for (const event of sorted) {
        if (event.start >= clusterEnd) flush();

        let lane = laneEnds.findIndex((end) => end <= event.start);
        if (lane === -1) {
            lane = laneEnds.length;
            laneEnds.push(event.end);
        } else {
            laneEnds[lane] = event.end;
        }

        cluster.push({ ...event, lane, lanes: 1 });
        clusterEnd = Math.max(clusterEnd, event.end);
    }
    flush();

    return result;
}
