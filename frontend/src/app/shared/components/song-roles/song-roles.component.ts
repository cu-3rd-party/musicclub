import { Component, inject, OnInit, signal, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TuiCardLarge } from '@taiga-ui/layout/components/card';
import { TuiButton } from '@taiga-ui/core/components/button';
import { TuiIcon } from '@taiga-ui/core/components/icon';
import { SongsService, type Song, type SongRole, type RoleAssignment } from '../../../core/services/songs.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
    selector: 'app-song-roles',
    standalone: true,
    imports: [
        CommonModule,
        TuiCardLarge,
        TuiButton,
        TuiIcon
    ],
    template: `
        @if (song(); as song) {
            <div class="song-roles">
                <h3 class="section-title">Участники</h3>
                
                <div class="roles-list">
                    @for (role of song.roles; track role.id) {
                        <div tuiCardLarge class="role-card">
                            <div class="role-header">
                                <tui-icon [icon]="getRoleIcon(role.name)" class="role-icon"></tui-icon>
                                <h4 class="role-name">{{ role.name }}</h4>
                            </div>
                            
                            @if (role.assignments && role.assignments.length > 0) {
                                <div class="role-assignments">
                                    @for (assignment of role.assignments; track assignment.id) {
                                        <div class="assignment-item">
                                            <div class="avatar-placeholder">
                                                {{ getInitials(assignment.userDisplayName) }}
                                            </div>
                                            <span class="assignment-name">{{ assignment.userDisplayName }}</span>
                                            
                                            @if (canRemoveAssignment(role, assignment)) {
                                                <button
                                                    tuiButton
                                                    size="xs"
                                                    appearance="flat"
                                                    (click)="onLeaveRole(role.id)"
                                                >
                                                    <tui-icon icon="@tui.x"></tui-icon>
                                                </button>
                                            }
                                        </div>
                                    }
                                </div>
                            } @else {
                                <p class="role-empty">Нет участников</p>
                            }
                            
                            <div class="role-actions">
                                @if (canJoinRole(role)) {
                                    <button
                                        tuiButton
                                        size="s"
                                        appearance="accent"
                                        (click)="onJoinRole(role.id)"
                                    >
                                        Присоединиться
                                    </button>
                                }
                                
                                @if (canLeaveRole(role)) {
                                    <button
                                        tuiButton
                                        size="s"
                                        appearance="flat"
                                        (click)="onLeaveRole(role.id)"
                                    >
                                        Покинуть
                                    </button>
                                }
                            </div>
                        </div>
                    }
                </div>
            </div>
        }
    `,
    styles: [`
        .song-roles {
            margin-top: 2rem;
        }
        
        .section-title {
            margin: 0 0 1.5rem;
            font-size: 1.25rem;
        }
        
        .roles-list {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 1rem;
        }
        
        .role-card {
            padding: 1.25rem;
        }
        
        .role-header {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            margin-bottom: 1rem;
            
            .role-icon {
                color: var(--tui-text-accent);
            }
            
            .role-name {
                margin: 0;
                font-size: 1rem;
                font-weight: 600;
            }
        }
        
        .role-assignments {
            display: flex;
            flex-direction: column;
            gap: 0.75rem;
            margin-bottom: 1rem;
        }
        
        .assignment-item {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 0.5rem;
            border-radius: var(--tui-radius-m);
            background-color: var(--tui-bg-neutral-1);
            
            .avatar-placeholder {
                width: 32px;
                height: 32px;
                border-radius: 50%;
                background-color: var(--tui-bg-base);
                color: var(--tui-text-accent);
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 0.75rem;
                font-weight: 600;
            }
        }
        
        .assignment-name {
            flex: 1;
            font-size: 0.9rem;
        }
        
        .role-empty {
            margin: 0 0 1rem;
            color: var(--tui-text-secondary);
            font-size: 0.9rem;
        }
        
        .role-actions {
            display: flex;
            gap: 0.5rem;
        }
    `]
})
export class SongRolesComponent implements OnInit {
    readonly song = input.required<Song>();
    
    private readonly songsService = inject<SongsService>(SongsService);
    private readonly toast = inject<ToastService>(ToastService);
    private readonly authService = inject<AuthService>(AuthService);
    
    currentUser = this.authService.getCurrentUserSync();

    ngOnInit(): void {
        // Refresh song data on init
    }

    getRoleIcon(roleName: string): string {
        const icons: Record<string, string> = {
            'Вокал': '@tui.mic',
            'Гитара': '@tui.guitar',
            'Бас': '@tui.bass',
            'Барабаны': '@tui.drum',
            'Клавиши': '@tui.piano',
            'Бэк-вокал': '@tui.users'
        };
        return icons[roleName] || '@tui.user';
    }

    getInitials(name: string): string {
        return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
    }

    canJoinRole(role: SongRole): boolean {
        if (!this.currentUser) return false;
        
        // Check if user is already assigned
        const isAssigned = role.assignments?.some((a: RoleAssignment) => a.userId === this.currentUser!.id);
        if (isAssigned) return false;
        
        // Check permissions
        return true; // Can join if not assigned
    }

    canLeaveRole(role: SongRole): boolean {
        if (!this.currentUser) return false;
        
        return role.assignments?.some((a: RoleAssignment) => a.userId === this.currentUser!.id);
    }

    canRemoveAssignment(role: SongRole, assignment: RoleAssignment): boolean {
        if (!this.currentUser) return false;
        
        // Can remove own assignment
        if (assignment.userId === this.currentUser.id) return true;
        
        // Can remove if has edit_any permission
        // TODO: Check permissions
        return false;
    }

    onJoinRole(roleId: string): void {
        this.songsService.joinRole(roleId).subscribe({
            next: () => {
                this.toast.show('Вы присоединились к роли', 'success');
                // TODO: Refresh song data
            },
            error: () => {
                this.toast.show('Ошибка присоединения к роли', 'error');
            }
        });
    }

    onLeaveRole(roleId: string): void {
        this.songsService.leaveRole(roleId).subscribe({
            next: () => {
                this.toast.show('Вы покинули роль', 'success');
                // TODO: Refresh song data
            },
            error: () => {
                this.toast.show('Ошибка выхода из роли', 'error');
            }
        });
    }
}
