import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastContainerComponent } from './shared/components/toast/toast-container.component';

@Component({
  imports: [RouterOutlet, ToastContainerComponent],
  selector: 'app-root',
  template: `
    <app-toast-container></app-toast-container>
    <router-outlet></router-outlet>
  `,
  styles: []
})
export class App {}
