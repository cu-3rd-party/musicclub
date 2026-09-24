import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { ToastService, type Toast, type ToastType } from '../../../core/services/toast.service';

@Component({
    selector: 'app-toast-container',
    standalone: true,
    imports: [CommonModule, TuiIcon],
    template: `
        <div class="toast-container">
            @for (toast of toasts; track toast.id) {
                <div class="toast toast-{{ toast.type }}">
                    <tui-icon 
                        [icon]="getIcon(toast.type)" 
                        class="toast-icon"
                    ></tui-icon>
                    <span class="toast-message">{{ toast.message }}</span>
                    <button class="toast-close" (click)="hideToast(toast.id)">
                        <tui-icon icon="@tui.x"></tui-icon>
                    </button>
                </div>
            }
        </div>
    `,
    styles: [`
        .toast-container {
            position: fixed;
            top: 1rem;
            right: 1rem;
            z-index: 9999;
            display: flex;
            flex-direction: column;
            gap: 0.75rem;
        }
        
        .toast {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 1rem 1.25rem;
            border-radius: var(--tui-radius-m);
            min-width: 300px;
            max-width: 500px;
            box-shadow: var(--tui-shadow-medium);
            animation: slideIn 0.3s ease-out;
            
            &.toast-success {
                background-color: var(--tui-bg-positive);
                color: var(--tui-text-on-color-positive);
            }
            
            &.toast-error {
                background-color: var(--tui-bg-negative);
                color: var(--tui-text-on-color-negative);
            }
            
            &.toast-info {
                background-color: var(--tui-bg-info);
                color: var(--tui-text-on-color-info);
            }
            
            &.toast-warning {
                background-color: var(--tui-bg-warning);
                color: var(--tui-text-on-color-warning);
            }
        }
        
        .toast-icon {
            flex-shrink: 0;
        }
        
        .toast-message {
            flex: 1;
            font-size: 0.95rem;
        }
        
        .toast-close {
            background: transparent;
            border: none;
            cursor: pointer;
            padding: 0.25rem;
            opacity: 0.7;
            
            &:hover {
                opacity: 1;
            }
        }
        
        @keyframes slideIn {
            from {
                transform: translateX(100%);
                opacity: 0;
            }
            to {
                transform: translateX(0);
                opacity: 1;
            }
        }
    `]
})
export class ToastContainerComponent {
    readonly toastService = inject<ToastService>(ToastService);

    get toasts(): Toast[] {
        return this.toastService.toasts();
    }

    hideToast(id: string): void {
        this.toastService.hide(id);
    }

    getIcon(type: string): string {
        switch (type) {
            case 'success': return '@tui.check-circle';
            case 'error': return '@tui.x-circle';
            case 'warning': return '@tui.alert-triangle';
            default: return '@tui.info';
        }
    }
}
