import { Component, inject, OnInit } from '@angular/core';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { AuthService } from '../../core/services/auth.service';
import type { UserProfile } from '../../shared/models/auth.models';

@Component({
    selector: 'app-profile',
    standalone: true,
    imports: [TuiCardLarge, TuiButton],
    template: `
        <div class="profile-page">
            <h1 class="page-title">Профиль</h1>
            
            @if (user) {
                <div tuiCardLarge class="profile-card">
                    <div class="profile-header">
                        <div class="avatar-placeholder">{{ user.displayName.charAt(0) }}</div>
                        <div class="profile-info">
                            <h3 class="profile-name">{{ user.displayName }}</h3>
                        </div>
                    </div>
                    
                    <button tuiButton appearance="destructive" (click)="onLogout()">
                        Выйти
                    </button>
                </div>
            }
        </div>
    `,
    styles: [`
        .profile-page {
            padding: 2rem;
        }
        
        .page-title {
            margin: 0 0 1.5rem 0;
            font-size: 1.75rem;
        }
        
        .profile-card {
            padding: 2rem;
            max-width: 500px;
        }
        
        .profile-header {
            display: flex;
            align-items: center;
            gap: 1.5rem;
            margin-bottom: 2rem;
        }
        
        .avatar-placeholder {
            width: 64px;
            height: 64px;
            border-radius: 50%;
            background-color: var(--tui-bg-base);
            color: var(--tui-text-accent);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.5rem;
            font-weight: 600;
        }
        
        .profile-name {
            margin: 0;
            font-size: 1.5rem;
        }
        
        .profile-info {
            flex: 1;
        }
    `]
})
export class ProfileComponent implements OnInit {
    private readonly authService = inject(AuthService);
    
    user: UserProfile | null = null;

    ngOnInit(): void {
        this.user = this.authService.getCurrentUserSync();
    }

    onLogout(): void {
        this.authService.logout();
    }
}
