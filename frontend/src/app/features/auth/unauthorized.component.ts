import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';

@Component({
    selector: 'app-unauthorized',
    standalone: true,
    imports: [TuiCardLarge, TuiButton, TuiIcon],
    template: `
        <div class="unauthorized-page">
            <div tuiCardLarge class="unauthorized-card">
                <div class="unauthorized-icon">
                    <tui-icon icon="@tui.lock"></tui-icon>
                </div>
                
                <h1 class="unauthorized-title">Доступ запрещён</h1>
                
                <p class="unauthorized-description">
                    Вам необходимо войти в систему для доступа к этой странице
                </p>
                
                <div class="unauthorized-actions">
                    <button 
                        tuiButton 
                        appearance="accent"
                        (click)="goToLogin()"
                    >
                        Войти
                    </button>
                    
                    <button 
                        tuiButton 
                        appearance="flat"
                        (click)="goBack()"
                    >
                        Назад
                    </button>
                </div>
            </div>
        </div>
    `,
    styles: [`
        .unauthorized-page {
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 2rem;
        }
        
        .unauthorized-card {
            padding: 3rem 2.5rem;
            text-align: center;
            max-width: 450px;
        }
        
        .unauthorized-icon {
            width: 80px;
            height: 80px;
            margin: 0 auto 1.5rem;
            border-radius: 50%;
            background-color: var(--tui-bg-warning);
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--tui-text-on-color-warning);
            
            tui-icon {
                font-size: 2.5rem;
            }
        }
        
        .unauthorized-title {
            margin: 0 0 1rem 0;
            font-size: 1.75rem;
        }
        
        .unauthorized-description {
            margin: 0 0 2rem;
            color: var(--tui-text-secondary);
        }
        
        .unauthorized-actions {
            display: flex;
            gap: 1rem;
            justify-content: center;
        }
    `]
})
export class UnauthorizedComponent {
    private readonly router = inject(Router);

    goToLogin(): void {
        this.router.navigate(['/auth/login']);
    }

    goBack(): void {
        history.back();
    }
}
