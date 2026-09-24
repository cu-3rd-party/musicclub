import { Injectable, inject, PLATFORM_ID, InjectionToken } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, BehaviorSubject, of, interval, Subscription } from 'rxjs';
import { catchError, tap, switchMap, filter, take } from 'rxjs/operators';
import { isPlatformBrowser } from '@angular/common';
import { environment } from '../../../environments/environment.development';
import type { AuthSession, TokenPair, UserProfile, TelegramUser } from '../../shared/models/auth.models';

const AUTH_STORAGE_KEY = 'auth.session';
const REFRESH_THRESHOLD_MS = 5 * 60 * 1000; // Refresh 5 minutes before expiry

export const TELEGRAM_INIT_DATA = new InjectionToken<string>('TELEGRAM_INIT_DATA');

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private readonly http = inject(HttpClient);
    private readonly platformId = inject(PLATFORM_ID);
    private readonly apiUrl = environment.API_URL;
    
    private readonly currentUserSubject = new BehaviorSubject<UserProfile | null>(null);
    public readonly currentUser$ = this.currentUserSubject.asObservable();
    
    private refreshTimer?: Subscription;

    constructor() {
        this.loadSessionFromStorage();
        this.startAutoRefresh();
    }

    /**
     * Аутентификация через Telegram WebApp initData
     */
    telegramAuth(initData: string): Observable<AuthSession> {
        return this.http.post<AuthSession>(`${this.apiUrl}/api/v1/auth/telegram`, { initData }).pipe(
            tap(session => this.saveSession(session)),
            catchError((error: HttpErrorResponse) => {
                const message = this.extractErrorMessage(error);
                return throwError(() => new Error(message));
            })
        );
    }

    /**
     * Аутентификация через deep link (uid из Telegram)
     */
    authViaDeeplink(uid: string): Observable<AuthSession> {
        return this.http.get<AuthSession>(`${this.apiUrl}/api/v1/auth/telegram/link/${uid}`).pipe(
            tap(session => this.saveSession(session)),
            catchError((error: HttpErrorResponse) => {
                if (error.status === 404 || error.status === 204) {
                    return throwError(() => new Error('Ссылка для входа не найдена или истекла'));
                }
                const message = this.extractErrorMessage(error);
                return throwError(() => new Error(message));
            })
        );
    }

    /**
     * Обновление токенов
     */
    refreshTokens(refreshToken: string): Observable<TokenPair> {
        return this.http.post<TokenPair>(`${this.apiUrl}/api/v1/auth/refresh`, { refreshToken }).pipe(
            tap(tokens => this.updateTokens(tokens)),
            catchError((error: HttpErrorResponse) => {
                // Если refresh token истек — выходим
                if (error.status === 401 || error.status === 403) {
                    this.logout();
                    return throwError(() => new Error('Сессия истекла. Пожалуйста, войдите снова.'));
                }
                return throwError(() => error);
            })
        );
    }

    /**
     * Получить текущего пользователя с сервера
     */
    getCurrentUser(): Observable<UserProfile> {
        return this.http.get<UserProfile>(`${this.apiUrl}/api/v1/auth/me`).pipe(
            tap(user => {
                this.currentUserSubject.next(user);
                const session = this.getSession();
                if (session) {
                    session.user = user;
                    this.saveSession(session);
                }
            }),
            catchError((error: HttpErrorResponse) => {
                if (error.status === 401) {
                    this.logout();
                    return throwError(() => new Error('Необходимо войти в систему'));
                }
                return throwError(() => error);
            })
        );
    }

    /**
     * Обновление профиля (имя, аватар)
     */
    updateProfile(name?: string, avatarFile?: File): Observable<UserProfile> {
        const formData = new FormData();
        if (name !== undefined) {
            formData.append('name', name);
        }
        if (avatarFile) {
            formData.append('avatar', avatarFile);
        }
        return this.http.patch<UserProfile>(`${this.apiUrl}/api/v1/auth/me`, formData).pipe(
            tap(user => {
                this.currentUserSubject.next(user);
                const session = this.getSession();
                if (session) {
                    session.user = user;
                    this.saveSession(session);
                }
            })
        );
    }

    /**
     * Выход из текущей сессии
     */
    logout(): void {
        this.stopAutoRefresh();
        this.clearSession();
        this.currentUserSubject.next(null);
    }

    /**
     * Выход из всех сессий
     */
    logoutAllSessions(): Observable<void> {
        return this.http.post<void>(`${this.apiUrl}/api/v1/auth/logout-all`, {}).pipe(
            tap(() => this.logout())
        );
    }

    /**
     * Проверка аутентификации
     */
    isAuthenticated(): boolean {
        const session = this.getSession();
        if (!session) return false;
        
        const now = new Date();
        const expiresAt = new Date(session.expiresAt);
        return now < expiresAt;
    }

    /**
     * Получить access token
     */
    getAccessToken(): string | null {
        const session = this.getSession();
        return session?.accessToken ?? null;
    }

    /**
     * Получить текущего пользователя синхронно (из localStorage)
     */
    getCurrentUserSync(): UserProfile | null {
        const session = this.getSession();
        return session?.user ?? null;
    }

    /**
     * Проверка, что токен скоро истечёт
     */
    isTokenExpiringSoon(): boolean {
        const session = this.getSession();
        if (!session) return false;
        
        const now = new Date();
        const expiresAt = new Date(session.expiresAt);
        const timeUntilExpiry = expiresAt.getTime() - now.getTime();
        
        return timeUntilExpiry < REFRESH_THRESHOLD_MS;
    }

    private saveSession(session: AuthSession): void {
        if (!isPlatformBrowser(this.platformId)) return;
        localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
        this.currentUserSubject.next(session.user);
        this.startAutoRefresh();
    }

    private updateTokens(tokens: TokenPair): void {
        const session = this.getSession();
        if (session) {
            session.accessToken = tokens.accessToken;
            session.refreshToken = tokens.refreshToken;
            session.expiresAt = tokens.expiresAt;
            this.saveSession(session);
        }
    }

    private getSession(): AuthSession | null {
        if (!isPlatformBrowser(this.platformId)) return null;
        
        const raw = localStorage.getItem(AUTH_STORAGE_KEY);
        if (!raw) return null;
        
        try {
            return JSON.parse(raw) as AuthSession;
        } catch {
            return null;
        }
    }

    private loadSessionFromStorage(): void {
        const session = this.getSession();
        if (session?.user) {
            this.currentUserSubject.next(session.user);
        }
    }

    private clearSession(): void {
        if (!isPlatformBrowser(this.platformId)) return;
        localStorage.removeItem(AUTH_STORAGE_KEY);
        this.currentUserSubject.next(null);
    }

    private startAutoRefresh(): void {
        this.stopAutoRefresh();
        
        // Проверяем каждые 30 секунд
        this.refreshTimer = interval(30000).subscribe(() => {
            if (this.isAuthenticated() && this.isTokenExpiringSoon()) {
                const session = this.getSession();
                if (session?.refreshToken) {
                    this.refreshTokens(session.refreshToken).subscribe({
                        error: () => {
                            // Refresh failed, user will be logged out
                            console.warn('Token refresh failed');
                        }
                    });
                }
            }
        });
    }

    private stopAutoRefresh(): void {
        if (this.refreshTimer) {
            this.refreshTimer.unsubscribe();
            this.refreshTimer = undefined;
        }
    }

    private extractErrorMessage(error: HttpErrorResponse): string {
        if (typeof error.error?.message === 'string') {
            return error.error.message;
        }
        if (error.status === 0) {
            return 'Сервер недоступен. Проверьте подключение к интернету.';
        }
        if (error.status === 500) {
            return 'Внутренняя ошибка сервера. Попробуйте позже.';
        }
        return error.message || 'Произошла ошибка. Попробуйте снова.';
    }
}
