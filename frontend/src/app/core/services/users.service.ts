import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';
import type { UserProfile } from '../../shared/models/auth.models';

@Injectable({
    providedIn: 'root'
})
export class UsersService {
    private readonly http = inject(HttpClient);
    private readonly apiUrl = environment.API_URL;

    getUsers(): Observable<UserProfile[]> {
        return this.http.get<UserProfile[]>(`${this.apiUrl}/api/v1/users`);
    }

    getUser(userId: string): Observable<UserProfile> {
        return this.http.get<UserProfile>(`${this.apiUrl}/api/v1/users/${userId}`);
    }

    getAssignments(userId: string): Observable<any> {
        return this.http.get<any>(`${this.apiUrl}/api/v1/users/${userId}/assignments`);
    }

    updatePreferences(userId: string, preferences: any): Observable<any> {
        return this.http.put<any>(`${this.apiUrl}/api/v1/users/${userId}/preferences`, preferences);
    }
}
