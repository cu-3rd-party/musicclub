import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { TuiInput } from '@taiga-ui/core/components/input';
import { TuiLoader } from '@taiga-ui/core/components/loader';
import { SongsService } from '../../core/services/songs.service';
import { DialogService } from '../../core/services/dialog.service';
import { ToastService } from '../../core/services/toast.service';
import { SongDialogComponent, type SongDialogData } from '../../shared/components/dialog/song-dialog.component';
import { ConfirmDialogComponent, type ConfirmDialogData } from '../../shared/components/dialog/confirm-dialog.component';
import type { Song, CreateSongPayload, ListSongsParams } from '../../shared/models/song.models';

@Component({
    selector: 'app-songs-list',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        TuiCardLarge,
        TuiButton,
        TuiIcon,
        TuiInput,
        TuiLoader,
        SongDialogComponent,
        ConfirmDialogComponent
    ],
    template: `
        <div class="songs-page">
            <div class="songs-header">
                <h1 class="page-title">Песни</h1>
                <button tuiButton appearance="accent" (click)="onCreateSong()">
                    <tui-icon icon="@tui.plus"></tui-icon>
                    Добавить песню
                </button>
            </div>
            
            <div class="songs-toolbar">
                <div class="search-box">
                    <tui-icon icon="@tui.search" class="search-icon"></tui-icon>
                    <input
                        tuiInput
                        type="text"
                        [(ngModel)]="searchQuery"
                        (ngModelChange)="onSearchChange()"
                        placeholder="Поиск по названию или исполнителю..."
                        class="search-input"
                    />
                </div>
            </div>
            
            @if (loading()) {
                <div class="loading-state">
                    <tui-loader size="l"></tui-loader>
                    <p>Загрузка песен...</p>
                </div>
            } @else if (songs().length === 0) {
                <div class="empty-state">
                    <tui-icon icon="@tui.music"></tui-icon>
                    <h3>Нет песен</h3>
                    <p>Добавьте первую песню, чтобы начать</p>
                </div>
            } @else {
                <div class="songs-grid">
                    @for (song of songs(); track song.id) {
                        <div tuiCardLarge class="song-card">
                            <div class="song-content">
                                <div class="song-main">
                                    <h3 class="song-title">{{ song.title }}</h3>
                                    <p class="song-artist">{{ song.artist }}</p>
                                </div>
                                
                                <div class="song-badges">
                                    @if (song.isFeatured) {
                                        <span class="badge badge-featured">
                                            <tui-icon icon="@tui.star"></tui-icon>
                                        </span>
                                    }
                                    @if (song.linkUrl) {
                                        <a [href]="song.linkUrl" target="_blank" class="badge badge-link">
                                            <tui-icon icon="@tui.external-link"></tui-icon>
                                        </a>
                                    }
                                </div>
                            </div>
                            
                            <div class="song-actions">
                                <button
                                    tuiButton
                                    size="s"
                                    appearance="flat"
                                    (click)="onEditSong(song)"
                                >
                                    <tui-icon icon="@tui.edit"></tui-icon>
                                </button>
                                <button
                                    tuiButton
                                    size="s"
                                    appearance="destructive"
                                    (click)="onDeleteSong(song)"
                                >
                                    <tui-icon icon="@tui.trash"></tui-icon>
                                </button>
                            </div>
                        </div>
                    }
                </div>
                
                @if (totalPages() > 1) {
                    <div class="pagination">
                        <button
                            tuiButton
                            appearance="flat"
                            [disabled]="page() <= 1"
                            (click)="changePage(page() - 1)"
                        >
                            Назад
                        </button>
                        
                        <span class="page-info">
                            Страница {{ page() }} из {{ totalPages() }}
                        </span>
                        
                        <button
                            tuiButton
                            appearance="flat"
                            [disabled]="page() >= totalPages()"
                            (click)="changePage(page() + 1)"
                        >
                            Вперёд
                        </button>
                    </div>
                }
            }
        </div>
        
        <app-song-dialog
            [data]="dialogService.songDialogData()!"
            [visible]="dialogService.songDialogVisible()"
            (close)="dialogService.closeSongDialog(null)"
            (submit)="dialogService.songDialogData()?.mode === 'create' ? onSongSubmit($event) : onEditSubmit($event)"
        ></app-song-dialog>
        
        <app-confirm-dialog
            [data]="dialogService.confirmDialogData()!"
            [visible]="dialogService.confirmDialogVisible()"
            (confirm)="dialogService.closeConfirmDialog(true)"
            (cancel)="dialogService.closeConfirmDialog(false)"
        ></app-confirm-dialog>
    `,
    styles: [`
        .songs-page {
            padding: 2rem;
        }
        
        .songs-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 2rem;
        }
        
        .page-title {
            margin: 0;
            font-size: 1.75rem;
        }
        
        .songs-toolbar {
            margin-bottom: 1.5rem;
        }
        
        .search-box {
            position: relative;
            max-width: 400px;
        }
        
        .search-icon {
            position: absolute;
            left: 1rem;
            top: 50%;
            transform: translateY(-50%);
            color: var(--tui-text-secondary);
            pointer-events: none;
        }
        
        .search-input {
            padding-left: 2.75rem;
        }
        
        .songs-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
            gap: 1.5rem;
        }
        
        .song-card {
            padding: 1.5rem;
            display: flex;
            flex-direction: column;
            gap: 1rem;
        }
        
        .song-content {
            display: flex;
            justify-content: space-between;
            align-items: flex-start;
            gap: 1rem;
        }
        
        .song-main {
            flex: 1;
            min-width: 0;
        }
        
        .song-title {
            margin: 0 0 0.5rem 0;
            font-size: 1.15rem;
            font-weight: 600;
        }
        
        .song-artist {
            margin: 0;
            color: var(--tui-text-secondary);
        }
        
        .song-badges {
            display: flex;
            gap: 0.5rem;
            flex-shrink: 0;
        }
        
        .badge {
            display: flex;
            align-items: center;
            justify-content: center;
            width: 32px;
            height: 32px;
            border-radius: 50%;
            
            &.badge-featured {
                background-color: var(--tui-bg-warning);
                color: var(--tui-text-on-color-warning);
            }
            
            &.badge-link {
                background-color: var(--tui-bg-info);
                color: var(--tui-text-on-color-info);
                text-decoration: none;
            }
        }
        
        .song-actions {
            display: flex;
            gap: 0.5rem;
            justify-content: flex-end;
            padding-top: 1rem;
            border-top: 1px solid var(--tui-border-normal);
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
        
        .pagination {
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 1.5rem;
            margin-top: 2rem;
        }
        
        .page-info {
            color: var(--tui-text-secondary);
        }
    `]
})
export class SongsListComponent implements OnInit {
    private readonly songsService = inject(SongsService);
    private readonly dialogService = inject(DialogService);
    private readonly toast = inject(ToastService);
    
    readonly loading = signal(false);
    readonly songs = signal<Song[]>([]);
    readonly total = signal(0);
    readonly page = signal(1);
    readonly pageSize = 12;
    readonly searchQuery = signal('');
    
    readonly totalPages = computed(() => Math.ceil(this.total() / this.pageSize));

    ngOnInit(): void {
        this.loadSongs();
    }

    loadSongs(): void {
        this.loading.set(true);
        const params: ListSongsParams = {
            page: this.page(),
            pageSize: this.pageSize,
            search: this.searchQuery() || undefined
        };
        
        this.songsService.getSongs(params).subscribe({
            next: (result) => {
                this.songs.set(result.items);
                this.total.set(result.total);
                this.loading.set(false);
            },
            error: () => {
                this.loading.set(false);
                this.toast.show('Ошибка загрузки песен', 'error');
            }
        });
    }

    onSearchChange(): void {
        this.page.set(1);
        this.loadSongs();
    }

    changePage(newPage: number): void {
        if (newPage < 1 || newPage > this.totalPages()) return;
        this.page.set(newPage);
        this.loadSongs();
    }

    async onCreateSong(): Promise<void> {
        this.dialogService.openSongDialog({ mode: 'create' });
    }

    onSongSubmit(payload: CreateSongPayload): void {
        this.dialogService.closeSongDialog(payload);
        
        this.songsService.createSong(payload).subscribe({
            next: () => {
                this.toast.show('Песня успешно создана', 'success');
                this.loadSongs();
            },
            error: () => {
                this.toast.show('Ошибка создания песни', 'error');
            }
        });
    }

    async onEditSong(song: Song): Promise<void> {
        this.dialogService.openSongDialog({ mode: 'edit', song });
    }

    onEditSubmit(payload: CreateSongPayload): void {
        const song = this.dialogService.songDialogData()?.song;
        if (!song) return;
        
        this.dialogService.closeSongDialog(payload);
        
        this.songsService.updateSong(song.id, payload).subscribe({
            next: () => {
                this.toast.show('Песня обновлена', 'success');
                this.loadSongs();
            },
            error: () => {
                this.toast.show('Ошибка обновления песни', 'error');
            }
        });
    }

    async onDeleteSong(song: Song): Promise<void> {
        const confirmed = await this.dialogService.confirmDelete(song.title);
        if (!confirmed) return;

        this.songsService.deleteSong(song.id).subscribe({
            next: () => {
                this.toast.show('Песня удалена', 'success');
                this.loadSongs();
            },
            error: () => {
                this.toast.show('Ошибка удаления песни', 'error');
            }
        });
    }
}
