import type {UUID} from "node:crypto";

export type UserProfile = {
    Id: UUID;
    email: string;
    name?: string;
    role: "admin" | "guest";
    emailVerified: boolean;
    avatarUrl?: string | null;
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

export type Deeplink = {
    url: string;
    uid: UUID;
}

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
    init_data: string;
};
