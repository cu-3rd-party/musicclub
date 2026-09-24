import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';
import { LayoutComponent } from './layout.component';

export const APP_ROUTES: Routes = [
    {
        path: '',
        component: LayoutComponent,
        canActivate: [authGuard],
        children: [
            {
                path: 'songs',
                loadChildren: () => import('../songs/songs.routes').then(m => m.SONGS_ROUTES)
            },
            {
                path: 'calendar',
                loadChildren: () => import('../calendar/calendar.routes').then(m => m.CALENDAR_ROUTES)
            },
            {
                path: 'profile',
                loadChildren: () => import('../profile/profile.routes').then(m => m.PROFILE_ROUTES)
            },
            {
                path: '',
                redirectTo: 'songs',
                pathMatch: 'full'
            }
        ]
    }
];
