import { Routes } from '@angular/router';
import { SongsListComponent } from './songs-list.component';

export const SONGS_ROUTES: Routes = [
    {
        path: '',
        component: SongsListComponent
    }
];
