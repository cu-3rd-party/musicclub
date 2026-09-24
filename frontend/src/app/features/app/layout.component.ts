import { Component, inject } from '@angular/core';
import { TuiRoot } from '@taiga-ui/core/components/root';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/services/auth.service';

@Component({
    selector: 'app-layout',
    standalone: true,
    imports: [TuiRoot, ShellComponent],
    template: `
        <tui-root>
            <app-shell></app-shell>
        </tui-root>
    `,
    styles: [`
        tui-root {
            height: 100vh;
        }
    `]
})
export class LayoutComponent {
    protected readonly authService = inject(AuthService);
}
