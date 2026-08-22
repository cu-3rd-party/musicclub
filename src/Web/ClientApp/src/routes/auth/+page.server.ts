import type { PageServerLoad } from "./$types";
import { createDeeplink } from "$lib/api/auth";

export const load: PageServerLoad = async () => {
    const deeplink = await createDeeplink();
    return {
        deeplink,
    };
};
