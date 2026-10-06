export type TimetableEventKind =
    "Rehearsal" | "Performance" | "Personal" | "External";

export type TimetableEvent = {
    id: string;
    title: string;
    kind: TimetableEventKind;
    startAt: Date;
    endAt: Date;
    start: number; // minutes after midnight (в пределах дня колонки)
    end: number; // minutes after midnight (в пределах дня колонки)
    songId: string | null;
    status: string | null;
    location: string | null;
    canDelete: boolean;
    syncedToCalendar: boolean | null;
};

/** Событие с раскладкой по дорожкам, если события пересекаются. */
export type PositionedTimetableEvent = TimetableEvent & {
    lane: number;
    lanes: number;
};
