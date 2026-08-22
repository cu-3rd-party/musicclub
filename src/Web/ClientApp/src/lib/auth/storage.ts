import { browser } from "$app/environment";

import type { AuthSession } from "$lib/auth/types";

const AUTH_STORAGE_KEY = "auth.session";

type StoredAuthSession = Partial<AuthSession> & {
    accessToken?: string;
    refreshToken?: string;
    expiresAt?: string;
};

export function getStoredAuthSession(): AuthSession | null {
    if (!browser) {
        return null;
    }

    const rawValue = window.localStorage.getItem(AUTH_STORAGE_KEY);
    if (!rawValue) {
        return null;
    }

    try {
        const session = normalizeStoredAuthSession(
            JSON.parse(rawValue) as StoredAuthSession,
        );
        if (!session) {
            window.localStorage.removeItem(AUTH_STORAGE_KEY);
            return null;
        }

        window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
        return session;
    } catch {
        window.localStorage.removeItem(AUTH_STORAGE_KEY);
        return null;
    }
}

export function setStoredAuthSession(session: AuthSession): void {
    if (!browser) {
        return;
    }

    window.localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

export function clearStoredAuthSession(): void {
    if (!browser) {
        return;
    }

    window.localStorage.removeItem(AUTH_STORAGE_KEY);
}

function normalizeStoredAuthSession(
    session: StoredAuthSession,
): AuthSession | null {
    if (
        !session.accessToken ||
        !session.refreshToken ||
        !session.expiresAt ||
        !session.user
    ) {
        return null;
    }

    return {
        accessToken: session.accessToken,
        refreshToken: session.refreshToken,
        expiresAt: session.expiresAt,
        accessTokenAcquiredAt:
            session.accessTokenAcquiredAt ?? new Date().toISOString(),
        user: session.user,
    };
}
