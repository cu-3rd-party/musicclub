import type { Deeplink } from "$lib/auth/types";

import { ssr_api } from "$lib/server/api";

export async function createDeeplink(): Promise<Deeplink> {
    const response = await ssr_api.get<Deeplink>("/api/v1/auth/telegram/link");
    return response.data;
}
