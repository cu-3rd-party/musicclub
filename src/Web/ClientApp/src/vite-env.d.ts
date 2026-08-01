/// <reference types="@sveltejs/kit" />

interface ImportMetaEnv {
    readonly API_URL?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}

type TelegramWebApp = {
    initData: string;
    initDataUnsafe?: Record<string, unknown>;
};

interface Window {
    Telegram?: { WebApp?: TelegramWebApp };
    TelegramWebviewProxy?: unknown;
}
