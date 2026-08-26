import type {Song} from "$lib/songs/types";

export type TimetableEvent = {
    id: string;
    song: Song;
    start: number; // minutes after midnight
    end: number; // minutes after midnight
};
