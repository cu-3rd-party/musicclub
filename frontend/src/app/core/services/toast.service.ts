import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
    id: string;
    type: ToastType;
    message: string;
    duration?: number;
}

@Injectable({
    providedIn: 'root'
})
export class ToastService {
    readonly toasts = signal<Toast[]>([]);

    show(message: string, type: ToastType = 'info', duration: number = 5000): void {
        const id = Math.random().toString(36).substring(2, 9);
        const toast: Toast = { id, type, message, duration };
        
        this.toasts.update(toasts => [...toasts, toast]);
        
        if (duration > 0) {
            setTimeout(() => this.hide(id), duration);
        }
    }

    success(message: string, duration: number = 5000): void {
        this.show(message, 'success', duration);
    }

    error(message: string, duration: number = 5000): void {
        this.show(message, 'error', duration);
    }

    info(message: string, duration: number = 5000): void {
        this.show(message, 'info', duration);
    }

    warning(message: string, duration: number = 5000): void {
        this.show(message, 'warning', duration);
    }

    hide(id: string): void {
        this.toasts.update(toasts => toasts.filter(t => t.id !== id));
    }

    clear(): void {
        this.toasts.set([]);
    }
}
