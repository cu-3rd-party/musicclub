import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';
import type { CalendarEvent, CalendarFeed } from '../../shared/models/calendar.models';

export type { CalendarEvent, CalendarFeed };

@Injectable({
    providedIn: 'root'
})
export class CalendarService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.API_URL;

    getCalendarEvents(): Observable<CalendarEvent[]> {
        return this.http.get<CalendarEvent[]>(`${this.apiUrl}/api/v1/calendar/events`);
    }

    getCalendarFeeds(): Observable<CalendarFeed[]> {
        return this.http.get<CalendarFeed[]>(`${this.apiUrl}/api/v1/calendar/feeds`);
    }

    addCalendarFeed(url: string, name: string): Observable<CalendarFeed> {
        return this.http.post<CalendarFeed>(`${this.apiUrl}/api/v1/calendar/feeds`, { url, name });
    }

    removeCalendarFeed(feedId: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/api/v1/calendar/feeds/${feedId}`);
    }

    toggleCalendarFeed(feedId: string, isEnabled: boolean): Observable<void> {
        return this.http.patch<void>(`${this.apiUrl}/api/v1/calendar/feeds/${feedId}`, { isEnabled });
    }
}
