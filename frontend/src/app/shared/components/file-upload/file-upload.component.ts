import { Component, inject, signal, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { TuiLoader } from '@taiga-ui/core/components/loader';
import { DataService, type DataEntry } from '../../../core/services/data.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
    selector: 'app-file-upload',
    standalone: true,
    imports: [
        CommonModule,
        TuiCardLarge,
        TuiButton,
        TuiIcon,
        TuiLoader
    ],
    template: `
        <div tuiCardLarge class="file-upload-section">
            <h3 class="section-title">
                <tui-icon icon="@tui.upload"></tui-icon>
                Загрузка файлов
            </h3>
            
            <div 
                class="drop-zone"
                [class.drop-zone-active]="isDragActive()"
                [class.drop-zone-error]="hasError()"
                (dragover)="onDragOver($event)"
                (dragleave)="onDragLeave($event)"
                (drop)="onDrop($event)"
                (click)="fileInput.click()"
            >
                <input
                    #fileInput
                    type="file"
                    hidden
                    (change)="onFileSelected($event)"
                    accept="image/*,.pdf,.doc,.docx"
                />
                
                @if (uploading()) {
                    <div class="uploading-state">
                        <tui-loader size="l"></tui-loader>
                        <p>Загрузка: {{ fileName() }}</p>
                        <div class="progress-bar">
                            <div 
                                class="progress-fill"
                                [style.width.%]="uploadProgress()"
                            ></div>
                        </div>
                    </div>
                } @else {
                    <div class="drop-content">
                        <tui-icon icon="@tui.cloud-upload" class="drop-icon"></tui-icon>
                        <p class="drop-text">
                            Перетащите файл сюда или <span class="browse-link">выберите</span>
                        </p>
                        <p class="drop-hint">
                            PNG, JPG, PDF, DOC до 10MB
                        </p>
                    </div>
                }
            </div>
            
            @if (hasError()) {
                <p class="error-message">{{ errorMessage() }}</p>
            }
            
            @if (uploadedFile()) {
                <div class="uploaded-file">
                    <div class="file-info">
                        <tui-icon icon="@tui.file" class="file-icon"></tui-icon>
                        <div class="file-details">
                            <span class="file-name">{{ uploadedFile()!.fileName }}</span>
                            <span class="file-size">{{ formatFileSize(uploadedFile()!.size) }}</span>
                        </div>
                    </div>
                    <div class="file-actions">
                        @if (uploadedFile()!.url) {
                            <a [href]="uploadedFile()!.url" target="_blank" class="file-link">
                                <tui-icon icon="@tui.external-link"></tui-icon>
                            </a>
                        }
                        <button
                            tuiButton
                            appearance="destructive"
                            size="s"
                            (click)="onDeleteFile()"
                        >
                            <tui-icon icon="@tui.trash"></tui-icon>
                        </button>
                    </div>
                </div>
            }
        </div>
    `,
    styles: [`
        .file-upload-section {
            margin-top: 1.5rem;
        }
        
        .section-title {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            margin: 0 0 1.5rem;
            font-size: 1.25rem;
        }
        
        .drop-zone {
            border: 2px dashed var(--tui-border-normal);
            border-radius: var(--tui-radius-l);
            padding: 3rem 2rem;
            text-align: center;
            cursor: pointer;
            transition: all 0.2s ease;
            
            &:hover {
                border-color: var(--tui-border-accent);
                background-color: var(--tui-bg-neutral-1);
            }
            
            &.drop-zone-active {
                border-color: var(--tui-border-accent);
                background-color: var(--tui-bg-info);
            }
            
            &.drop-zone-error {
                border-color: var(--tui-border-negative);
                background-color: var(--tui-bg-negative);
            }
        }
        
        .drop-content {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 1rem;
        }
        
        .drop-icon {
            font-size: 3rem;
            color: var(--tui-text-secondary);
        }
        
        .drop-text {
            margin: 0;
            font-size: 1.1rem;
        }
        
        .browse-link {
            color: var(--tui-text-accent);
            font-weight: 500;
            text-decoration: underline;
        }
        
        .drop-hint {
            margin: 0;
            color: var(--tui-text-secondary);
            font-size: 0.9rem;
        }
        
        .uploading-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 1.5rem;
        }
        
        .progress-bar {
            width: 100%;
            max-width: 400px;
            height: 8px;
            background-color: var(--tui-bg-neutral-1);
            border-radius: var(--tui-radius-m);
            overflow: hidden;
        }
        
        .progress-fill {
            height: 100%;
            background-color: var(--tui-bg-info);
            transition: width 0.3s ease;
        }
        
        .error-message {
            margin: 1rem 0 0;
            padding: 0.75rem 1rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-negative);
            color: var(--tui-text-on-color-negative);
            font-size: 0.9rem;
        }
        
        .uploaded-file {
            margin-top: 1.5rem;
            padding: 1rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-positive);
            color: var(--tui-text-on-color-positive);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        
        .file-info {
            display: flex;
            align-items: center;
            gap: 0.75rem;
        }
        
        .file-icon {
            font-size: 1.5rem;
        }
        
        .file-details {
            display: flex;
            flex-direction: column;
            gap: 0.25rem;
        }
        
        .file-name {
            font-weight: 500;
        }
        
        .file-size {
            font-size: 0.85rem;
            opacity: 0.8;
        }
        
        .file-actions {
            display: flex;
            gap: 0.5rem;
        }
    `]
})
export class FileUploadComponent {
    private readonly dataService = inject<DataService>(DataService);
    private readonly toast = inject<ToastService>(ToastService);
    
    readonly uploading = signal(false);
    readonly uploadProgress = signal(0);
    readonly fileName = signal('');
    readonly uploadedFile = signal<DataEntry | null>(null);
    readonly isDragActive = signal(false);
    readonly hasError = signal(false);
    readonly errorMessage = signal('');
    
    private selectedFile: File | null = null;
    private maxFileSize = 10 * 1024 * 1024; // 10MB

    onFileSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        const file = input.files?.[0];
        if (file) {
            this.handleFile(file);
        }
    }

    onDragOver(event: DragEvent): void {
        event.preventDefault();
        this.isDragActive.set(true);
    }

    onDragLeave(event: DragEvent): void {
        event.preventDefault();
        this.isDragActive.set(false);
    }

    onDrop(event: DragEvent): void {
        event.preventDefault();
        this.isDragActive.set(false);
        
        const files = event.dataTransfer?.files;
        if (files && files.length > 0) {
            this.handleFile(files[0]);
        }
    }

    private handleFile(file: File): void {
        this.hasError.set(false);
        this.errorMessage.set('');
        
        // Validate file size
        if (file.size > this.maxFileSize) {
            this.hasError.set(true);
            this.errorMessage.set('Файл слишком большой. Максимальный размер: 10MB');
            return;
        }
        
        // Validate file type
        const allowedTypes = ['image/png', 'image/jpeg', 'image/gif', 'application/pdf'];
        if (!allowedTypes.includes(file.type)) {
            this.hasError.set(true);
            this.errorMessage.set('Неподдерживаемый формат файла');
            return;
        }
        
        this.selectedFile = file;
        this.uploadFile(file);
    }

    private uploadFile(file: File): void {
        this.uploading.set(true);
        this.fileName.set(file.name);
        this.uploadProgress.set(0);
        
        // Simulate progress
        const progressInterval = setInterval(() => {
            const progress = this.uploadProgress();
            if (progress < 90) {
                this.uploadProgress.set(progress + 10);
            }
        }, 200);
        
        this.dataService.uploadFile(file).subscribe({
            next: (entry: DataEntry) => {
                clearInterval(progressInterval);
                this.uploadProgress.set(100);
                this.uploading.set(false);
                this.uploadedFile.set(entry);
                this.toast.show('Файл успешно загружен', 'success');
            },
            error: (error: unknown) => {
                clearInterval(progressInterval);
                this.uploading.set(false);
                this.hasError.set(true);
                this.errorMessage.set('Ошибка загрузки файла');
                this.toast.show('Ошибка загрузки файла', 'error');
            }
        });
    }

    onDeleteFile(): void {
        const file = this.uploadedFile();
        if (!file) return;
        
        this.dataService.deleteFile(file.id).subscribe({
            next: () => {
                this.uploadedFile.set(null);
                this.toast.show('Файл удалён', 'success');
            },
            error: () => {
                this.toast.show('Ошибка удаления файла', 'error');
            }
        });
    }

    private formatFileSize(bytes: number): string {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
}
