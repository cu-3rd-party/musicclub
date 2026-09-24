import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TuiIcon } from '@taiga-ui/core/components/icon';

@Component({
    selector: 'app-sidebar',
    standalone: true,
    imports: [RouterLink, RouterLinkActive, TuiIcon],
    template: `
        <aside class="sidebar">
            <div class="sidebar-header">
                <span class="logo">Music Club</span>
            </div>
            
            <nav class="sidebar-nav">
                <a 
                    routerLink="/app/songs" 
                    routerLinkActive="active"
                    class="nav-item"
                >
                    <tui-icon icon="@tui.music"></tui-icon>
                    <span>Песни</span>
                </a>
                
                <a 
                    routerLink="/app/calendar" 
                    routerLinkActive="active"
                    class="nav-item"
                >
                    <tui-icon icon="@tui.calendar"></tui-icon>
                    <span>Календарь</span>
                </a>
                
                <a 
                    routerLink="/app/profile" 
                    routerLinkActive="active"
                    class="nav-item"
                >
                    <tui-icon icon="@tui.user"></tui-icon>
                    <span>Профиль</span>
                </a>
            </nav>
        </aside>
    `,
    styles: [`
        .sidebar {
            width: 260px;
            background-color: var(--tui-bg-neutral-1);
            border-right: 1px solid var(--tui-border-neutral-2);
            display: flex;
            flex-direction: column;
        }
        
        .sidebar-header {
            padding: 1.5rem 1rem;
            font-size: 1.25rem;
            font-weight: 600;
            border-bottom: 1px solid var(--tui-border-neutral-2);
        }
        
        .sidebar-nav {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
            padding: 1rem;
        }
        
        .nav-item {
            display: flex;
            align-items: center;
            gap: 0.75rem;
            padding: 0.75rem 1rem;
            border-radius: var(--tui-radius-m);
            text-decoration: none;
            color: var(--tui-text-primary);
            transition: background-color 0.2s;
            
            &:hover {
                background-color: var(--tui-bg-neutral-hover);
            }
            
            &.active {
                background-color: var(--tui-bg-base);
                color: var(--tui-text-accent);
            }
        }
    `]
})
export class AppSidebarComponent {}
