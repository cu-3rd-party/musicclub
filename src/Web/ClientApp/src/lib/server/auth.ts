import type { ExternalProvider } from "$lib/auth/types";

import { ssr_api } from "$lib/server/api";

export async function listExternalProviders(): Promise<ExternalProvider[]> {
    const response = await ssr_api.get<ExternalProvider[]>(
        "/api/v1/auth/providers",
    );
    return response.data;
}
