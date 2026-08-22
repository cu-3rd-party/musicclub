import { writable } from "svelte/store";

import {
    getCurrentUser,
    logoutCurrentSession,
    refreshAuthTokens,
    telegramAuth,
    updateUserProfile,
    isUnauthorizedError,
} from "$lib/api/auth";
import {
    clearStoredAuthSession,
    getStoredAuthSession,
    setStoredAuthSession,
} from "$lib/auth/storage";
import type { AuthSession, UserProfile } from "$lib/auth/types";

type AuthState = {
    user: UserProfile | null;
    ready: boolean;
};

const ACCESS_TOKEN_REFRESH_INTERVAL_MS = 10 * 60 * 1000;

const initialSession = getStoredAuthSession();
let refreshIntervalId: number | null = null;
let refreshInFlight: Promise<boolean> | null = null;

export const authState = writable<AuthState>({
    user: initialSession?.user ?? null,
    ready: false,
});

export function hydrateAuthState(): void {
    authState.set({
        user: getStoredAuthSession()?.user ?? null,
        ready: true,
    });

    if (getStoredAuthSession()) {
        startAccessTokenRefreshLoop();
    } else {
        stopAccessTokenRefreshLoop();
    }
}

export async function telegramLogin(
    initData: string,
): Promise<UserProfile | null> {
    try {
        const session = await telegramAuth({ init_data: initData });
        const persistedSession: AuthSession = {
            ...session,
            accessTokenAcquiredAt: new Date().toISOString(),
        };
        persistSession(persistedSession);
        return persistedSession.user;
    } catch {
        return null;
    }
}

export async function ensureAuthenticated(): Promise<boolean> {
    const storedSession = getStoredAuthSession();
    if (!storedSession?.user) {
        clearAuth();
        return false;
    }

    startAccessTokenRefreshLoop();

    if (shouldRefreshAccessToken(storedSession)) {
        const refreshed = await refreshStoredSession(storedSession);
        if (!refreshed) {
            return false;
        }
    }

    const activeSession = getStoredAuthSession() ?? storedSession;
    authState.set({ user: activeSession.user, ready: false });

    try {
        const user = await getCurrentUser();
        persistSession({ ...(getStoredAuthSession() ?? storedSession), user });
        return true;
    } catch (error) {
        if (isUnauthorizedError(error) && storedSession.refreshToken) {
            const refreshed = await refreshStoredSession(storedSession);
            if (!refreshed) {
                clearAuth();
                return false;
            }

            try {
                const user = await getCurrentUser();
                persistSession({ ...refreshed, user });
                return true;
            } catch {
                clearAuth();
                return false;
            }
        }

        if (isUnauthorizedError(error)) {
            clearAuth();
            return false;
        }

        authState.set({ user: storedSession.user, ready: true });
        return true;
    }
}

export async function logout(): Promise<void> {
    const storedSession = getStoredAuthSession();
    try {
        await logoutCurrentSession(storedSession?.refreshToken);
    } catch {
        // Local cleanup should still happen if the session is already gone server-side.
    }
    clearAuth();
}

export async function updateProfile(
    name?: string,
    avatarFile?: File,
): Promise<UserProfile> {
    await updateUserProfile(name, avatarFile);
    const user = await getCurrentUser();
    const storedSession = getStoredAuthSession();
    if (storedSession) {
        persistSession({ ...storedSession, user });
    }
    return user;
}

function persistSession(session: AuthSession): void {
    setStoredAuthSession(session);
    startAccessTokenRefreshLoop();
    authState.set({ user: session.user, ready: true });
}

function clearAuth(): void {
    stopAccessTokenRefreshLoop();
    clearStoredAuthSession();
    authState.set({ user: null, ready: true });
}

function startAccessTokenRefreshLoop(): void {
    if (refreshIntervalId) {
        return;
    }

    refreshIntervalId = window.setInterval(() => {
        void refreshStoredSessionIfNeeded();
    }, ACCESS_TOKEN_REFRESH_INTERVAL_MS);
}

function stopAccessTokenRefreshLoop(): void {
    if (!refreshIntervalId) {
        return;
    }

    window.clearInterval(refreshIntervalId);
    refreshIntervalId = null;
}

function shouldRefreshAccessToken(session: AuthSession): boolean {
    return (
        Date.now() - new Date(session.accessTokenAcquiredAt).getTime() >=
        ACCESS_TOKEN_REFRESH_INTERVAL_MS
    );
}

async function refreshStoredSessionIfNeeded(): Promise<boolean> {
    const storedSession = getStoredAuthSession();
    if (!storedSession) {
        stopAccessTokenRefreshLoop();
        return false;
    }

    if (!shouldRefreshAccessToken(storedSession)) {
        return true;
    }

    return (await refreshStoredSession(storedSession)) !== null;
}

async function refreshStoredSession(
    session: AuthSession,
): Promise<AuthSession | null> {
    if (refreshInFlight) {
        const didRefresh = await refreshInFlight;
        return didRefresh ? getStoredAuthSession() : null;
    }

    refreshInFlight = (async () => {
        try {
            const tokenPair = await refreshAuthTokens(session.refreshToken);
            const refreshedSession: AuthSession = {
                ...session,
                ...tokenPair,
                accessTokenAcquiredAt: new Date().toISOString(),
            };
            setStoredAuthSession(refreshedSession);
            authState.set({ user: refreshedSession.user, ready: true });
            return true;
        } catch {
            clearAuth();
            return false;
        } finally {
            refreshInFlight = null;
        }
    })();

    const didRefresh = await refreshInFlight;
    return didRefresh ? getStoredAuthSession() : null;
}
