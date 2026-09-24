import { Component, signal, input, output, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { TuiCheckbox } from '@taiga-ui/core/components/checkbox';
import type { CreateSongPayload, SongLinkKind, Song } from '../../../shared/models/song.models';

export interface SongDialogData {
    song?: Song;
    mode: 'create' | 'edit';
}

@Component({
    selector: 'app-song-dialog',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        TuiButton,
        TuiIcon,
        TuiCheckbox
    ],
    template: `
        @if (visible()) {
            <div class="dialog-overlay" (click)="onCancel()">
                <div class="dialog-content" (click)="$event.stopPropagation()">
                    <h2 class="dialog-title">
                        {{ data().mode === 'create' ? 'Новая песня' : 'Редактирование' }}
                    </h2>
                    
                    <form (ngSubmit)="onSubmit()" class="song-form">
                        <div class="form-group">
                            <label>Название *</label>
                            <input
                                tuiInput
                                type="text"
                                [(ngModel)]="formData.title"
                                name="title"
                                required
                                placeholder="Например: Bohemian Rhapsody"
                                class="form-input"
                            />
                        </div>
                        
                        <div class="form-group">
                            <label>Исполнитель *</label>
                            <input
                                tuiInput
                                type="text"
                                [(ngModel)]="formData.artist"
                                name="artist"
                                required
                                placeholder="Например: Queen"
                                class="form-input"
                            />
                        </div>
                        
                        <div class="form-group">
                            <label>Тип ссылки</label>
                            <select
                                [(ngModel)]="formData.linkKind"
                                name="linkKind"
                                class="form-input"
                            >
                                <option value="external">Внешняя</option>
                                <option value="youtube">YouTube</option>
                                <option value="spotify">Spotify</option>
                                <option value="soundcloud">SoundCloud</option>
                                <option value="bandcamp">Bandcamp</option>
                            </select>
                        </div>
                        
                        <div class="form-group">
                            <label>URL</label>
                            <input
                                tuiInput
                                type="url"
                                [(ngModel)]="formData.linkUrl"
                                name="linkUrl"
                                placeholder="https://..."
                                class="form-input"
                            />
                        </div>
                        
                        <div class="form-group checkbox-group">
                            <label class="checkbox-label">
                                <input
                                    type="checkbox"
                                    tuiCheckbox
                                    [(ngModel)]="formData.isFeatured"
                                    name="isFeatured"
                                />
                                Избранное
                            </label>
                        </div>
                        
                        <div class="dialog-actions">
                            <button
                                type="button"
                                tuiButton
                                appearance="flat"
                                (click)="onCancel()"
                            >
                                Отмена
                            </button>
                            <button
                                type="submit"
                                tuiButton
                                appearance="accent"
                                [disabled]="!isValid()"
                            >
                                {{ data().mode === 'create' ? 'Создать' : 'Сохранить' }}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        }
    `,
    styles: [`
        .dialog-overlay {
            position: fixed;
            inset: 0;
            background-color: rgba(0, 0, 0, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1000;
            animation: fadeIn 0.2s ease-out;
        }
        
        .dialog-content {
            background: var(--tui-bg-base);
            padding: 2rem;
            border-radius: var(--tui-radius-l);
            max-width: 500px;
            width: 90%;
            max-height: 90vh;
            overflow-y: auto;
            animation: slideUp 0.3s ease-out;
        }
        
        .dialog-title {
            margin: 0 0 1.5rem;
            font-size: 1.5rem;
        }
        
        .song-form {
            display: flex;
            flex-direction: column;
            gap: 1.25rem;
        }
        
        .form-group {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
            
            label {
                font-size: 0.9rem;
                font-weight: 500;
            }
        }
        
        .form-input {
            width: 100%;
            padding: 0.75rem 1rem;
            border: 1px solid var(--tui-border-normal);
            border-radius: var(--tui-radius-m);
            background: var(--tui-bg-base);
            color: var(--tui-text-primary);
            font-size: 1rem;
            
            &:focus {
                outline: none;
                border-color: var(--tui-border-accent);
            }
        }
        
        select.form-input {
            cursor: pointer;
        }
        
        .checkbox-group {
            flex-direction: row;
            align-items: center;
        }
        
        .checkbox-label {
            display: flex;
            align-items: center;
            gap: 0.5rem;
            cursor: pointer;
        }
        
        .dialog-actions {
            display: flex;
            justify-content: flex-end;
            gap: 1rem;
            margin-top: 1rem;
        }
        
        @keyframes fadeIn {
            from { opacity: 0; }
            to { opacity: 1; }
        }
        
        @keyframes slideUp {
            from {
                transform: translateY(20px);
                opacity: 0;
            }
            to {
                transform: translateY(0);
                opacity: 1;
            }
        }
    `]
})
export class SongDialogComponent {
    readonly data = input.required<SongDialogData>();
    readonly visible = input.required<boolean>();
    readonly close = output<void>();
    readonly submit = output<CreateSongPayload>();
    
    formData: CreateSongPayload = {
        title: '',
        artist: '',
        linkKind: 'external',
        linkUrl: '',
        isFeatured: false
    };

    constructor() {
        effect(() => {
            const d = this.data();
            this.formData = {
                title: d.song?.title || '',
                artist: d.song?.artist || '',
                linkKind: d.song?.linkKind || 'external',
                linkUrl: d.song?.linkUrl || '',
                isFeatured: d.song?.isFeatured || false
            };
        });
    }

    isValid(): boolean {
        return !!(this.formData.title?.trim() && this.formData.artist?.trim());
    }

    onSubmit(): void {
        if (!this.isValid()) return;
        
        this.submit.emit({
            title: this.formData.title.trim(),
            artist: this.formData.artist.trim(),
            linkKind: this.formData.linkKind as SongLinkKind,
            linkUrl: this.formData.linkUrl?.trim(),
            isFeatured: this.formData.isFeatured
        });
    }

    onCancel(): void {
        this.close.emit();
    }
}
