import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { LoadingComponent } from '../../shared/components/loading/loading.component';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';
import { ToastContainerComponent } from '../../shared/components/toast/toast-container.component';

declare global {
    interface Window {
        Telegram?: {
            WebApp: {
                initData: string;
                MainButton: {
                    setText: (text: string) => void;
                    onClick: (callback: () => void) => void;
                    off: (callback: () => void) => void;
                    show: () => void;
                    hide: () => void;
                    enable: () => void;
                };
                ready: () => void;
            };
        };
    }
}

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [TuiCardLarge, TuiButton, TuiIcon, LoadingComponent],
    template: `
        <div class="login-page">
            @if (loading()) {
                <app-loading [overlay]="true" size="l" description="Вход в систему..."></app-loading>
            }
            
            <div class="login-content">
                <div tuiCardLarge class="login-card">
                    <div class="login-icon">
                        <tui-icon icon="@tui.send"></tui-icon>
                    </div>
                    
                    <h1 class="login-title">Music Club</h1>
                    
                    <p class="login-description">
                        Войдите через Telegram для доступа к песням, событиям и расписанию
                    </p>
                    
                    @if (errorMessage()) {
                        <div class="error-message">
                            <tui-icon icon="@tui.alert-circle"></tui-icon>
                            <span>{{ errorMessage() }}</span>
                        </div>
                    }
                    
                    <button 
                        tuiButton 
                        appearance="accent" 
                        size="l"
                        [disabled]="loading()"
                        (click)="onTelegramLogin()"
                        class="login-button"
                    >
                        <tui-icon icon="@tui.send"></tui-icon>
                        Войти через Telegram
                    </button>
                    
                    <p class="login-hint">
                        Нажмите кнопку и разрешите доступ к вашему профилю
                    </p>
                </div>
            </div>
        </div>
    `,
    styles: [`
        .login-page {
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 2rem;
            background: linear-gradient(135deg, var(--tui-bg-base) 0%, var(--tui-bg-neutral-1) 100%);
        }
        
        .login-content {
            width: 100%;
            max-width: 450px;
        }
        
        .login-card {
            padding: 3rem 2.5rem;
            text-align: center;
        }
        
        .login-icon {
            width: 80px;
            height: 80px;
            margin: 0 auto 1.5rem;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--tui-bg-info) 0%, var(--tui-bg-info-hover) 100%);
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--tui-text-on-color-info);
            
            tui-icon {
                font-size: 2.5rem;
            }
        }
        
        .login-title {
            margin: 0 0 1rem 0;
            font-size: 2rem;
            font-weight: 700;
        }
        
        .login-description {
            margin: 0 0 2rem;
            color: var(--tui-text-secondary);
            line-height: 1.5;
        }
        
        .error-message {
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 0.5rem;
            padding: 0.75rem 1rem;
            margin-bottom: 1.5rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-negative);
            color: var(--tui-text-on-color-negative);
            font-size: 0.9rem;
        }
        
        .login-button {
            width: 100%;
            padding: 0.875rem 1.5rem;
            font-size: 1rem;
            font-weight: 500;
        }
        
        .login-hint {
            margin: 1.5rem 0 0;
            font-size: 0.85rem;
            color: var(--tui-text-tertiary);
        }
    `]
})
export class LoginComponent implements OnInit, OnDestroy {
    private readonly authService = inject(AuthService);
    private readonly router = inject(Router);
    private readonly route = inject(ActivatedRoute);
    private readonly toast = inject(ToastService);
    
    readonly loading = signal(false);
    readonly errorMessage = signal<string | null>(null);
    
    private tgWebApp?: any;
    private mainButtonHandler?: () => void;

    ngOnInit(): void {
        // Проверяем наличие deeplink в URL
        this.route.queryParams.subscribe(params => {
            const uid = params['uid'];
            if (uid) {
                this.authViaDeeplink(uid);
            }
        });
        
        // Инициализируем Telegram WebApp
        this.initTelegramWebApp();
        
        // Если уже авторизован — перенаправляем
        if (this.authService.isAuthenticated()) {
            this.router.navigate(['/app/songs']);
        }
    }

    ngOnDestroy(): void {
        if (this.tgWebApp?.MainButton && this.mainButtonHandler) {
            this.tgWebApp.MainButton.off(this.mainButtonHandler);
        }
    }

    private initTelegramWebApp(): void {
        if (window.Telegram?.WebApp) {
            this.tgWebApp = window.Telegram.WebApp;
            this.tgWebApp.ready();
            
            // Настраиваем главную кнопку
            this.tgWebApp.MainButton.setText('ВОЙТИ В MUSIC CLUB');
            this.mainButtonHandler = this.handleMainButtonClick;
            this.tgWebApp.MainButton.onClick(this.mainButtonHandler);
        }
    }

    private handleMainButtonClick = (): void => {
        const initData = this.tgWebApp?.initData;
        if (initData) {
            this.authenticateWithTelegram(initData);
        }
    };

    onTelegramLogin(): void {
        if (this.tgWebApp?.initData) {
            // Запускаем из Telegram — показываем главную кнопку
            this.tgWebApp.MainButton.show();
            this.tgWebApp.MainButton.enable();
            this.toast.show('Нажмите кнопку внизу экрана для входа', 'info');
        } else {
            // Запускаем из браузера — пробуем получить initData
            this.toast.show('Откройте приложение через Telegram для входа', 'warning');
        }
    }

    private authenticateWithTelegram(initData: string): void {
        this.loading.set(true);
        this.authService.telegramAuth(initData).subscribe({
            next: () => {
                this.loading.set(false);
                this.toast.show('Успешный вход!', 'success');
                this.tgWebApp?.MainButton.hide();
                this.router.navigate(['/app/songs']);
            },
            error: (error: Error) => {
                this.loading.set(false);
                this.toast.show(error.message, 'error');
                this.tgWebApp?.MainButton.hide();
            }
        });
    }

    private authViaDeeplink(uid: string): void {
        this.loading.set(true);
        this.authService.authViaDeeplink(uid).subscribe({
            next: () => {
                this.loading.set(false);
                this.toast.show('Успешный вход!', 'success');
                this.router.navigate(['/app/songs']);
            },
            error: (error: Error) => {
                this.loading.set(false);
                this.toast.show(error.message, 'error');
            }
        });
    }
}
