export interface UserProfile {
    id: string;
    displayName: string;
    avatarUrl?: string;
    tgUserId?: string;
}

export interface TokenPair {
    accessToken: string;
    refreshToken: string;
    expiresAt: string;
}

export interface AuthSession extends TokenPair {
    accessTokenAcquiredAt: string;
    user: UserProfile;
}

export interface TelegramInitDataPayload {
    initData: string;
}

export interface Deeplink {
    uid: string;
}

export interface TelegramUser {
    id: number;
    first_name: string;
    last_name?: string;
    username?: string;
    language_code?: string;
    is_premium?: boolean;
}

export interface TelegramInitData {
    query_id?: string;
    user?: TelegramUser;
    auth_date: number;
    hash: string;
    start_param?: string;
}
