import type { UUID } from "node:crypto";

export type UserProfile = {
    id: UUID;
    displayName: string;
    username: string | null;
    avatarUrl: string | null;
    permissions: string[];
    lastLoginAt?: string | null;
    createdAt: string;
    updatedAt: string;
};

export type TokenPair = {
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
};

export type AuthSession = TokenPair & {
    accessTokenAcquiredAt: string;
    user: UserProfile;
};

export type ExternalProvider = {
    name: string;
    displayName: string;
};

export type LoginPayload = {
    email: string;
    password: string;
};

export type RegisterPayload = {
    email: string;
    name: string;
    password: string;
};

export type TelegramInitDataPayload = {
    initData: string;
};
