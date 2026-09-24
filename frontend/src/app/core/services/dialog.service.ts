import { Injectable, signal } from '@angular/core';
import type { SongDialogData } from '../../shared/components/dialog/song-dialog.component';
import type { ConfirmDialogData } from '../../shared/components/dialog/confirm-dialog.component';
import type { CreateSongPayload } from '../../shared/models/song.models';

@Injectable({
    providedIn: 'root'
})
export class DialogService {
    readonly songDialogData = signal<SongDialogData | null>(null);
    readonly songDialogVisible = signal(false);
    readonly songDialogResult = signal<CreateSongPayload | null>(null);
    
    readonly confirmDialogData = signal<ConfirmDialogData | null>(null);
    readonly confirmDialogVisible = signal(false);
    readonly confirmDialogResult = signal<boolean>(false);

    openSongDialog(data: SongDialogData): Promise<CreateSongPayload | null> {
        this.songDialogResult.set(null);
        this.songDialogData.set(data);
        this.songDialogVisible.set(true);
        
        return new Promise((resolve) => {
            const checkResult = setInterval(() => {
                if (!this.songDialogVisible()) {
                    clearInterval(checkResult);
                    resolve(this.songDialogResult());
                }
            }, 100);
        });
    }

    closeSongDialog(result: CreateSongPayload | null): void {
        this.songDialogResult.set(result);
        this.songDialogVisible.set(false);
    }

    openConfirmDialog(data: ConfirmDialogData): Promise<boolean> {
        this.confirmDialogResult.set(false);
        this.confirmDialogData.set(data);
        this.confirmDialogVisible.set(true);
        
        return new Promise((resolve) => {
            const checkResult = setInterval(() => {
                if (!this.confirmDialogVisible()) {
                    clearInterval(checkResult);
                    resolve(this.confirmDialogResult());
                }
            }, 100);
        });
    }

    closeConfirmDialog(result: boolean): void {
        this.confirmDialogResult.set(result);
        this.confirmDialogVisible.set(false);
    }

    async confirmDelete(title: string): Promise<boolean> {
        return this.openConfirmDialog({
            title: 'Удалить песню?',
            message: `Вы уверены, что хотите удалить "${title}"? Это действие нельзя отменить.`,
            confirmText: 'Удалить',
            appearance: 'destructive'
        });
    }
}
