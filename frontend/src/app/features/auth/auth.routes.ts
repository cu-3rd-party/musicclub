import { Routes } from '@angular/router';
import { LoginComponent } from './login.component';
import { UnauthorizedComponent } from './unauthorized.component';
import { publicGuard } from '../../core/guards/auth.guard';

export const AUTH_ROUTES: Routes = [
    {
        path: 'login',
        component: LoginComponent,
        canActivate: [publicGuard]
    },
    {
        path: 'unauthorized',
        component: UnauthorizedComponent
    },
    {
        path: '**',
        redirectTo: 'login'
    }
];
