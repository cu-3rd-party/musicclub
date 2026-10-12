import type { PageServerLoad } from "./$types";
import { listExternalProviders } from "$lib/server/auth";

export const load: PageServerLoad = async ({ url }) => {
    const providers = await listExternalProviders();
    return {
        providers,
        error: url.searchParams.get("error"),
    };
};
