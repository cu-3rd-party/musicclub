import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { TuiLoader } from '@taiga-ui/core/components/loader';
import { CalendarService, type CalendarEvent, type CalendarFeed } from '../../core/services/calendar.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
    selector: 'app-calendar',
    standalone: true,
    imports: [
        CommonModule,
        TuiCardLarge,
        TuiButton,
        TuiIcon,
        TuiLoader
    ],
    template: `
        <div class="calendar-page">
            <div class="calendar-header">
                <h1 class="page-title">Календарь</h1>
                
                <div class="header-actions">
                    <button tuiButton appearance="accent" (click)="toggleView()">
                        <tui-icon [icon]="view() === 'list' ? '@tui.calendar' : '@tui.layout-grid'"></tui-icon>
                        {{ view() === 'list' ? 'Список' : 'Сетка' }}
                    </button>
                </div>
            </div>
            
            @if (loading()) {
                <div class="loading-state">
                    <tui-loader size="l"></tui-loader>
                    <p>Загрузка событий...</p>
                </div>
            } @else {
                @if (view() === 'list') {
                    <div class="events-list">
                        @for (event of events(); track event.id) {
                            <div tuiCardLarge class="event-card">
                                <div class="event-header">
                                    <div class="event-time-badge">
                                        <span class="time-date">{{ event.startTime | date:'d' }}</span>
                                        <span class="time-month">{{ event.startTime | date:'MMM' }}</span>
                                    </div>
                                    <div class="event-info">
                                        <h3 class="event-title">{{ event.title }}</h3>
                                        @if (event.description) {
                                            <p class="event-description">{{ event.description }}</p>
                                        }
                                    </div>
                                </div>
                                
                                <div class="event-details">
                                    <div class="detail-item">
                                        <tui-icon icon="@tui.clock"></tui-icon>
                                        <span>{{ event.startTime | date:'HH:mm' }} - {{ event.endTime | date:'HH:mm' }}</span>
                                    </div>
                                    
                                    @if (event.location) {
                                        <div class="detail-item">
                                            <tui-icon icon="@tui.map-pin"></tui-icon>
                                            <span>{{ event.location }}</span>
                                        </div>
                                    }
                                </div>
                            </div>
                        }
                        
                        @if (events().length === 0) {
                            <div class="empty-state">
                                <tui-icon icon="@tui.calendar"></tui-icon>
                                <h3>Нет событий</h3>
                                <p>В ближайшее время событий не запланировано</p>
                            </div>
                        }
                    </div>
                } @else {
                    <div class="events-grid">
                        @for (event of events(); track event.id) {
                            <div tuiCardLarge class="grid-event">
                                <div class="grid-event-header">
                                    <span class="grid-event-date">
                                        {{ event.startTime | date:'d MMM' }}
                                    </span>
                                    <span class="grid-event-time">
                                        {{ event.startTime | date:'HH:mm' }}
                                    </span>
                                </div>
                                <h4 class="grid-event-title">{{ event.title }}</h4>
                                @if (event.location) {
                                    <p class="grid-event-location">{{ event.location }}</p>
                                }
                            </div>
                        }
                        
                        @if (events().length === 0) {
                            <div class="empty-state">
                                <tui-icon icon="@tui.calendar"></tui-icon>
                                <h3>Нет событий</h3>
                                <p>В ближайшее время событий не запланировано</p>
                            </div>
                        }
                    </div>
                }
            }
        </div>
    `,
    styles: [`
        .calendar-page {
            padding: 2rem;
        }
        
        .calendar-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 2rem;
        }
        
        .page-title {
            margin: 0;
            font-size: 1.75rem;
        }
        
        .header-actions {
            display: flex;
            gap: 0.5rem;
        }
        
        .loading-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 4rem 2rem;
            gap: 1rem;
            color: var(--tui-text-secondary);
        }
        
        .events-list {
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }
        
        .event-card {
            padding: 1.5rem;
        }
        
        .event-header {
            display: flex;
            gap: 1.5rem;
            margin-bottom: 1rem;
        }
        
        .event-time-badge {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-width: 60px;
            padding: 0.75rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-info);
            color: var(--tui-text-on-color-info);
        }
        
        .time-date {
            font-size: 1.5rem;
            font-weight: 700;
        }
        
        .time-month {
            font-size: 0.85rem;
            text-transform: uppercase;
        }
        
        .event-info {
            flex: 1;
        }
        
        .event-title {
            margin: 0 0 0.5rem;
            font-size: 1.25rem;
        }
        
        .event-description {
            margin: 0;
            color: var(--tui-text-secondary);
        }
        
        .event-details {
            display: flex;
            gap: 1.5rem;
            padding-top: 1rem;
            border-top: 1px solid var(--tui-border-normal);
        }
        
        .detail-item {
            display: flex;
            align-items: center;
            gap: 0.5rem;
            color: var(--tui-text-secondary);
            font-size: 0.9rem;
        }
        
        .empty-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 4rem 2rem;
            gap: 1rem;
            color: var(--tui-text-secondary);
            text-align: center;
            
            tui-icon {
                font-size: 4rem;
                opacity: 0.5;
            }
            
            h3 {
                margin: 0;
                font-size: 1.25rem;
            }
            
            p {
                margin: 0;
            }
        }
        
        .events-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
            gap: 1rem;
        }
        
        .grid-event {
            padding: 1.25rem;
        }
        
        .grid-event-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 0.75rem;
        }
        
        .grid-event-date {
            font-size: 0.9rem;
            color: var(--tui-text-secondary);
        }
        
        .grid-event-time {
            font-size: 0.85rem;
            padding: 0.25rem 0.5rem;
            border-radius: var(--tui-radius-s);
            background-color: var(--tui-bg-info);
            color: var(--tui-text-on-color-info);
        }
        
        .grid-event-title {
            margin: 0 0 0.5rem;
            font-size: 1.1rem;
        }
        
        .grid-event-location {
            margin: 0;
            color: var(--tui-text-secondary);
            font-size: 0.9rem;
        }
    `]
})
export class CalendarComponent implements OnInit {
    private readonly calendarService = inject<CalendarService>(CalendarService);
    private readonly toast = inject<ToastService>(ToastService);
    
    readonly loading = signal(false);
    readonly events = signal<CalendarEvent[]>([]);
    readonly view = signal<'list' | 'grid'>('list');

    ngOnInit(): void {
        this.loadEvents();
    }

    loadEvents(): void {
        this.loading.set(true);
        this.calendarService.getCalendarEvents().subscribe({
            next: (events) => {
                this.events.set(events);
                this.loading.set(false);
            },
            error: () => {
                this.loading.set(false);
                this.toast.show('Ошибка загрузки событий', 'error');
            }
        });
    }

    toggleView(): void {
        this.view.set(this.view() === 'list' ? 'grid' : 'list');
    }
}
