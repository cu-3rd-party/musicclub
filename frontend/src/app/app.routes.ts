import { Routes } from '@angular/router';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from './core/services/auth.service';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'app/songs',
        pathMatch: 'full'
    },
    {
        path: 'auth',
        loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
    },
    {
        path: 'app',
        loadChildren: () => import('./features/app/app.routes').then(m => m.APP_ROUTES),
        data: { requiresAuth: true }
    },
    {
        path: '**',
        redirectTo: 'app/songs'
    }
];
