import { api } from "$lib/api/client";

export type CalendarEventType = "Rehearsal" | "Performance" | "Personal";

export interface CalendarFeed {
    userId: string;
    feedToken: string;
    icsUrl: string;
    isActive: boolean;
    createdAt: string;
    revokedAt?: string;
}

export interface CalendarEvent {
    id: string;
    userId: string;
    title: string;
    description?: string;
    startAt: string;
    endAt: string;
    location?: string;
    eventType: CalendarEventType;
    sourceType: string;
    sourceId?: string;
}

export interface CreateCalendarEventRequest {
    title: string;
    description?: string;
    startAt: string;
    endAt: string;
    location?: string;
    eventType: CalendarEventType;
}

/**
 * Получить свой календарный фид
 */
export async function getCalendarFeed(): Promise<CalendarFeed> {
    const response = await api.get<CalendarFeed>("/api/v1/calendar/feed");
    return response.data;
}

/**
 * Перевыпустить токен календарного фида
 */
export async function regenerateCalendarFeed(): Promise<CalendarFeed> {
    const response = await api.post<CalendarFeed>(
        "/api/v1/calendar/feed/regenerate",
    );
    return response.data;
}

/**
 * Отозвать календарный фид
 */
export async function revokeCalendarFeed(): Promise<void> {
    await api.delete("/api/v1/calendar/feed");
}

/**
 * Получить события календаря
 * @param from Дата начала периода (ISO 8601)
 * @param to Дата окончания периода (ISO 8601)
 */
export async function getCalendarEvents(
    from?: string,
    to?: string,
): Promise<CalendarEvent[]> {
    const response = await api.get<CalendarEvent[]>("/api/v1/calendar/events", {
        params: { from, to },
    });
    return response.data;
}

/**
 * Создать личное событие в календаре
 */
export async function createCalendarEvent(
    event: CreateCalendarEventRequest,
): Promise<CalendarEvent> {
    const response = await api.post<CalendarEvent>(
        "/api/v1/calendar/events",
        event,
    );
    return response.data;
}

/**
 * Удалить личное событие из календаря
 */
export async function deleteCalendarEvent(eventId: string): Promise<void> {
    await api.delete(`/api/v1/calendar/events/${eventId}`);
}
