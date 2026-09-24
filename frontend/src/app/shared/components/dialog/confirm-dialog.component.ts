import { Component, input, output } from '@angular/core';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';

export interface ConfirmDialogData {
    title: string;
    message: string;
    confirmText?: string;
    cancelText?: string;
    appearance?: 'destructive' | 'accent';
}

@Component({
    selector: 'app-confirm-dialog',
    standalone: true,
    imports: [TuiButton, TuiIcon],
    template: `
        @if (visible()) {
            <div class="dialog-overlay" (click)="onCancel()">
                <div class="dialog-content" (click)="$event.stopPropagation()">
                    <div class="confirm-icon">
                        <tui-icon icon="@tui.alert-triangle"></tui-icon>
                    </div>
                    
                    <h2 class="confirm-title">{{ data().title }}</h2>
                    <p class="confirm-message">{{ data().message }}</p>
                    
                    <div class="confirm-actions">
                        <button
                            type="button"
                            tuiButton
                            appearance="flat"
                            (click)="onCancel()"
                        >
                            {{ data().cancelText || 'Отмена' }}
                        </button>
                        <button
                            type="button"
                            tuiButton
                            [appearance]="data().appearance || 'destructive'"
                            (click)="onConfirm()"
                        >
                            {{ data().confirmText || 'Удалить' }}
                        </button>
                    </div>
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
            max-width: 400px;
            width: 90%;
            text-align: center;
            animation: slideUp 0.3s ease-out;
        }
        
        .confirm-icon {
            width: 64px;
            height: 64px;
            margin: 0 auto 1.5rem;
            border-radius: 50%;
            background-color: var(--tui-bg-warning);
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--tui-text-on-color-warning);
            
            tui-icon {
                font-size: 2rem;
            }
        }
        
        .confirm-title {
            margin: 0 0 1rem;
            font-size: 1.25rem;
        }
        
        .confirm-message {
            margin: 0 0 1.5rem;
            color: var(--tui-text-secondary);
        }
        
        .confirm-actions {
            display: flex;
            gap: 1rem;
            justify-content: center;
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
export class ConfirmDialogComponent {
    readonly data = input.required<ConfirmDialogData>();
    readonly visible = input.required<boolean>();
    readonly confirm = output<void>();
    readonly cancel = output<void>();

    onConfirm(): void {
        this.confirm.emit();
    }

    onCancel(): void {
        this.cancel.emit();
    }
}
