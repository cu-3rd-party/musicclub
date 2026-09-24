import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';
import type {
    Song,
    CreateSongPayload,
    UpdateSongPayload,
    ListSongsParams,
    ListSongsResult,
    RoleCandidates,
    RolePayload,
    SongRole,
    RoleAssignment,
    RoadieAssignment,
    UserProfile
} from '../../shared/models/song.models';

export type {
    Song,
    CreateSongPayload,
    UpdateSongPayload,
    ListSongsParams,
    ListSongsResult,
    RoleCandidates,
    RolePayload,
    SongRole,
    RoleAssignment,
    RoadieAssignment,
    UserProfile
};

@Injectable({
    providedIn: 'root'
})
export class SongsService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.API_URL;

    getSongs(params?: ListSongsParams): Observable<ListSongsResult> {
        let httpParams = new HttpParams();
        if (params) {
            if (params.page) httpParams = httpParams.set('page', params.page.toString());
            if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
            if (params.search) httpParams = httpParams.set('search', params.search);
            if (params.featured !== undefined) httpParams = httpParams.set('featured', params.featured.toString());
        }
        return this.http.get<ListSongsResult>(`${this.apiUrl}/api/v1/songs`, { params: httpParams });
    }

    getSong(songId: string): Observable<Song> {
        return this.http.get<Song>(`${this.apiUrl}/api/v1/songs/${songId}`);
    }

    createSong(payload: CreateSongPayload): Observable<Song> {
        return this.http.post<Song>(`${this.apiUrl}/api/v1/songs`, payload);
    }

    updateSong(songId: string, payload: UpdateSongPayload): Observable<Song> {
        return this.http.put<Song>(`${this.apiUrl}/api/v1/songs/${songId}`, payload);
    }

    deleteSong(songId: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/api/v1/songs/${songId}`);
    }

    joinRole(roleId: string, payload?: RolePayload): Observable<Song> {
        return this.http.post<Song>(`${this.apiUrl}/api/v1/songs/roles/${roleId}/join`, payload || {});
    }

    leaveRole(roleId: string, payload?: RolePayload): Observable<Song> {
        return this.http.post<Song>(`${this.apiUrl}/api/v1/songs/roles/${roleId}/leave`, payload || {});
    }

    getRoleCandidates(
        songId: string,
        roleId: string,
        query?: string,
        mode?: 'assign' | 'remove'
    ): Observable<RoleCandidates> {
        let params = new HttpParams();
        if (query) params = params.set('query', query);
        if (mode) params = params.set('mode', mode);
        return this.http.get<RoleCandidates>(
            `${this.apiUrl}/api/v1/songs/${songId}/roles/${roleId}/candidates`,
            { params }
        );
    }

    callRoadie(songId: string): Observable<void> {
        return this.http.post<void>(`${this.apiUrl}/api/v1/songs/${songId}/roadie-ticket`, {});
    }

    getRoadieCandidates(songId: string, query?: string): Observable<RoleCandidates> {
        let params = new HttpParams();
        if (query) params = params.set('query', query);
        return this.http.get<RoleCandidates>(
            `${this.apiUrl}/api/v1/songs/${songId}/roadie/candidates`,
            { params }
        );
    }

    assignRoadie(songId: string, payload: RolePayload): Observable<Song> {
        return this.http.post<Song>(`${this.apiUrl}/api/v1/songs/${songId}/roadie`, payload);
    }

    removeRoadie(songId: string): Observable<Song> {
        return this.http.delete<Song>(`${this.apiUrl}/api/v1/songs/${songId}/roadie`);
    }
}
