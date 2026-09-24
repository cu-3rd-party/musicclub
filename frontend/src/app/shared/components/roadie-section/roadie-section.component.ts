import { Component, inject, OnInit, signal, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { TuiInput } from '@taiga-ui/core/components/input';
import { SongsService, type Song, type UserProfile, type RoleCandidates } from '../../../core/services/songs.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
    selector: 'app-roadie-section',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        TuiCardLarge,
        TuiButton,
        TuiIcon,
        TuiInput
    ],
    template: `
        @if (song(); as song) {
            <div tuiCardLarge class="roadie-section">
                <div class="roadie-header">
                    <div class="roadie-title">
                        <tui-icon icon="@tui.wrench"></tui-icon>
                        <h3>Роуди</h3>
                    </div>
                    
                    @if (song.roadie) {
                        <div class="roadie-assigned">
                            <div class="avatar-placeholder">
                                {{ getInitials(song.roadie.userDisplayName) }}
                            </div>
                            <span class="roadie-name">{{ song.roadie.userDisplayName }}</span>
                            
                            @if (canRemoveRoadie()) {
                                <button
                                    tuiButton
                                    size="s"
                                    appearance="destructive"
                                    (click)="onRemoveRoadie()"
                                >
                                    <tui-icon icon="@tui.trash"></tui-icon>
                                </button>
                            }
                        </div>
                    } @else {
                        @if (canCallRoadie()) {
                            <button
                                tuiButton
                                appearance="accent"
                                (click)="onCallRoadie()"
                            >
                                <tui-icon icon="@tui.bell"></tui-icon>
                                Позвать роуди
                            </button>
                        } @else {
                            <p class="roadie-waiting">Ожидание назначения роуди...</p>
                        }
                    }
                </div>
                
                @if (showAssignDialog()) {
                    <div class="assign-dialog-overlay" (click)="closeAssignDialog()">
                        <div class="assign-dialog" (click)="$event.stopPropagation()">
                            <h4>Назначить роуди</h4>
                            
                            <div class="search-box">
                                <tui-icon icon="@tui.search" class="search-icon"></tui-icon>
                                <input
                                    tuiInput
                                    type="text"
                                    [(ngModel)]="searchQuery"
                                    (ngModelChange)="onSearchChange()"
                                    placeholder="Поиск участника..."
                                    class="search-input"
                                />
                            </div>
                            
                            <div class="candidates-list">
                                @for (candidate of candidates(); track candidate.id) {
                                    <div class="candidate-item" (click)="onAssignRoadie(candidate)">
                                        <div class="avatar-placeholder">
                                            {{ getInitials(candidate.displayName) }}
                                        </div>
                                        <span class="candidate-name">{{ candidate.displayName }}</span>
                                    </div>
                                }
                                
                                @if (candidates().length === 0) {
                                    <p class="no-candidates">Нет доступных кандидатов</p>
                                }
                            </div>
                        </div>
                    </div>
                }
            </div>
        }
    `,
    styles: [`
        .roadie-section {
            margin-top: 1.5rem;
        }
        
        .roadie-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        
        .roadie-title {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            
            h3 {
                margin: 0;
                font-size: 1.1rem;
            }
            
            tui-icon {
                color: var(--tui-text-accent);
            }
        }
        
        .roadie-assigned {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 0.5rem 1rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-neutral-1);
            
            .avatar-placeholder {
                width: 32px;
                height: 32px;
                border-radius: 50%;
                background-color: var(--tui-bg-base);
                color: var(--tui-text-accent);
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 0.85rem;
                font-weight: 600;
            }
        }
        
        .roadie-name {
            font-weight: 500;
        }
        
        .roadie-waiting {
            margin: 0;
            color: var(--tui-text-secondary);
            font-size: 0.9rem;
        }
        
        .assign-dialog-overlay {
            position: fixed;
            inset: 0;
            background-color: rgba(0, 0, 0, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
        }
        
        .assign-dialog {
            background: var(--tui-bg-base);
            padding: 1.5rem;
            border-radius: var(--tui-radius-l);
            max-width: 400px;
            width: 90%;
            max-height: 80vh;
            overflow-y: auto;
        }
        
        .assign-dialog h4 {
            margin: 0 0 1rem;
        }
        
        .search-box {
            position: relative;
            margin-bottom: 1rem;
        }
        
        .search-icon {
            position: absolute;
            left: 1rem;
            top: 50%;
            transform: translateY(-50%);
            color: var(--tui-text-secondary);
        }
        
        .search-input {
            padding-left: 2.75rem;
            width: 100%;
        }
        
        .candidates-list {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
        }
        
        .candidate-item {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 0.75rem;
            border-radius: var(--tui-radius-m);
            cursor: pointer;
            transition: background-color 0.2s;
            
            .avatar-placeholder {
                width: 40px;
                height: 40px;
                border-radius: 50%;
                background-color: var(--tui-bg-base);
                color: var(--tui-text-accent);
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 0.9rem;
                font-weight: 600;
            }
            
            &:hover {
                background-color: var(--tui-bg-neutral-1);
            }
        }
        
        .candidate-name {
            flex: 1;
        }
        
        .no-candidates {
            text-align: center;
            color: var(--tui-text-secondary);
            padding: 1rem;
        }
    `]
})
export class RoadieSectionComponent implements OnInit {
    readonly song = input.required<Song>();
    readonly roadieAssigned = output<UserProfile>();
    readonly roadieRemoved = output<void>();
    
    private readonly songsService = inject<SongsService>(SongsService);
    private readonly toast = inject<ToastService>(ToastService);
    
    readonly showAssignDialog = signal(false);
    readonly searchQuery = signal('');
    readonly candidates = signal<UserProfile[]>([]);

    ngOnInit(): void {
        // Load candidates when dialog opens
    }

    getInitials(name: string): string {
        return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
    }

    canCallRoadie(): boolean {
        const song = this.song();
        return !song.roadie;
    }

    canRemoveRoadie(): boolean {
        // Can remove if current user assigned the roadie or is the roadie
        // TODO: Check permissions
        return true;
    }

    onCallRoadie(): void {
        this.songsService.callRoadie(this.song().id).subscribe({
            next: () => {
                this.toast.show('Роуди вызван! Ожидание назначения...', 'info');
            },
            error: () => {
                this.toast.show('Ошибка вызова роуди', 'error');
            }
        });
    }

    onRemoveRoadie(): void {
        this.songsService.removeRoadie(this.song().id).subscribe({
            next: () => {
                this.toast.show('Роуди удалён', 'success');
                this.roadieRemoved.emit();
            },
            error: () => {
                this.toast.show('Ошибка удаления роуди', 'error');
            }
        });
    }

    openAssignDialog(): void {
        this.showAssignDialog.set(true);
        this.loadCandidates();
    }

    closeAssignDialog(): void {
        this.showAssignDialog.set(false);
        this.searchQuery.set('');
    }

    loadCandidates(): void {
        this.songsService.getRoadieCandidates(this.song().id).subscribe({
            next: (result: RoleCandidates) => {
                this.candidates.set(result.candidates);
            },
            error: () => {
                this.candidates.set([]);
            }
        });
    }

    onSearchChange(): void {
        const query = this.searchQuery();
        if (query.length >= 2) {
            this.songsService.getRoadieCandidates(this.song().id, query).subscribe({
                next: (result: RoleCandidates) => {
                    this.candidates.set(result.candidates);
                }
            });
        } else if (query.length === 0) {
            this.loadCandidates();
        }
    }

    onAssignRoadie(candidate: UserProfile): void {
        this.songsService.assignRoadie(this.song().id, { userId: candidate.id }).subscribe({
            next: () => {
                this.toast.show('Роуди назначен', 'success');
                this.closeAssignDialog();
                this.roadieAssigned.emit(candidate);
            },
            error: () => {
                this.toast.show('Ошибка назначения роуди', 'error');
            }
        });
    }
}
