import axios from "axios";

import {api} from "$lib/api/client";
import type {
    AuthSession,
    LoginPayload,
    RegisterPayload,
    TokenPair,
    UserProfile,
    TelegramInitDataPayload, Deeplink,
} from "$lib/auth/types";

type ApiErrorPayload = {
    message?: string;
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

export async function createDeeplink(): Promise<Deeplink> {
    const response = await api.get<Deeplink>(
        "/api/v1/auth/telegram/link"
    );
    return response.data;
}

export async function getDeeplink(link: Deeplink): Promise<AuthSession | null> {
    try {
        const response = await api.get<AuthSession>(
            `/api/v1/auth/telegram/link/${link.uid}`
        );
        return response.data;
    } catch (e) {
        return null;
    }
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
    await api.post("/api/v1/auth/logout", refreshToken ? {refreshToken} : {});
}

export async function logoutAllSessions(): Promise<void> {
    await api.post("/api/v1/auth/logout-all");
}

export function getApiErrorMessage(
    error: unknown,
    fallbackMessage: string,
): string {
    if (axios.isAxiosError<ApiErrorPayload>(error)) {
        return error.response?.data?.message ?? fallbackMessage;
    }

    if (error instanceof Error && error.message) {
        return error.message;
    }

    return fallbackMessage;
}

export function isUnauthorizedError(error: unknown): boolean {
    return axios.isAxiosError(error) && (error.response?.status === 401 || error.response?.status === 404);
}
