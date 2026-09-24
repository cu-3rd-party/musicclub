import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppSidebarComponent } from '../../shared/components/app-sidebar.component';

@Component({
    selector: 'app-shell',
    standalone: true,
    imports: [RouterOutlet, AppSidebarComponent],
    template: `
        <div class="app-shell">
            <app-sidebar></app-sidebar>
            <main class="main-content">
                <router-outlet></router-outlet>
            </main>
        </div>
    `,
    styles: [`
        .app-shell {
            display: flex;
            height: 100%;
        }
        
        .main-content {
            flex: 1;
            overflow: auto;
        }
    `]
})
export class ShellComponent {}
