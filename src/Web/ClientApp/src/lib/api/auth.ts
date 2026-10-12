import axios from "axios";

import { api } from "$lib/api/client";
import type {
    AuthSession,
    TokenPair,
    UserProfile,
    TelegramInitDataPayload,
} from "$lib/auth/types";
import { API_URL } from "$lib/config";

type ApiErrorPayload = {
    message?: string;
    // ValidationProblemDetails: { errors: { field: ["сообщение"] } }
    errors?: Record<string, string[]>;
};

export async function telegramAuth(
    payload: TelegramInitDataPayload,
): Promise<AuthSession> {
    const response = await api.post<AuthSession>(
        "/api/v1/auth/telegram",
        payload,
    );
    return response.data;
}

/** Адрес, с которого бэкенд уводит на страницу входа провайдера (полная навигация, не XHR). */
export function externalLoginUrl(provider: string, returnUrl: string): string {
    const query = new URLSearchParams({ returnUrl });
    return `${API_URL}/api/v1/auth/external/${encodeURIComponent(provider)}?${query}`;
}

/** Одноразовый код с /auth/callback → сессия. */
export async function exchangeExternalLoginCode(
    code: string,
): Promise<AuthSession> {
    const response = await api.post<AuthSession>(
        "/api/v1/auth/external/exchange",
        { code },
    );
    return response.data;
}

export async function refreshAuthTokens(
    refreshToken: string,
): Promise<TokenPair> {
    const response = await api.post<TokenPair>("/api/v1/auth/refresh", {
        refreshToken,
    });
    return response.data;
}

export async function getCurrentUser(): Promise<UserProfile> {
    const response = await api.get<UserProfile>("/api/v1/auth/me");
    return response.data;
}

export async function updateUserProfile(
    name?: string,
    avatarFile?: File,
): Promise<UserProfile> {
    const formData = new FormData();
    if (name !== undefined) {
        formData.append("name", name);
    }
    if (avatarFile) {
        formData.append("avatar", avatarFile);
    }
    const response = await api.patch<UserProfile>("/api/v1/auth/me", formData);
    return response.data;
}

export async function logoutCurrentSession(
    refreshToken?: string,
): Promise<void> {
    await api.post("/api/v1/auth/logout", refreshToken ? { refreshToken } : {});
}

export async function logoutAllSessions(): Promise<void> {
    await api.post("/api/v1/auth/logout-all");
}

export function getApiErrorMessage(
    error: unknown,
    fallbackMessage: string,
): string {
    if (axios.isAxiosError<ApiErrorPayload>(error)) {
        const data = error.response?.data;
        const validationMessage = data?.errors
            ? Object.values(data.errors).flat()[0]
            : undefined;
        return data?.message ?? validationMessage ?? fallbackMessage;
    }

    if (error instanceof Error && error.message) {
        return error.message;
    }

    return fallbackMessage;
}

export function isUnauthorizedError(error: unknown): boolean {
    return (
        axios.isAxiosError(error) &&
        (error.response?.status === 401 || error.response?.status === 404)
    );
}
