import { Component, Input } from '@angular/core';
import { TuiLoader } from '@taiga-ui/core/components/loader';

@Component({
    selector: 'app-loading',
    standalone: true,
    imports: [TuiLoader],
    template: `
        <div class="loading-container" [class.loading-overlay]="overlay">
            <tui-loader [size]="size"></tui-loader>
        </div>
    `,
    styles: [`
        .loading-container {
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 2rem;
        }
        
        .loading-overlay {
            position: fixed;
            inset: 0;
            background-color: rgba(0, 0, 0, 0.5);
            z-index: 9998;
        }
    `]
})
export class LoadingComponent {
    @Input() overlay = false;
    @Input() size: 's' | 'm' | 'l' | 'xl' = 'm';
}
