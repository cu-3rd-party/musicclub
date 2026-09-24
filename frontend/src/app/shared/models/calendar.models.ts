export interface CalendarEvent {
    id: string;
    title: string;
    description?: string;
    startTime: string;
    endTime: string;
    location?: string;
}

export interface CalendarFeed {
    id: string;
    name: string;
    url: string;
    isEnabled: boolean;
}
