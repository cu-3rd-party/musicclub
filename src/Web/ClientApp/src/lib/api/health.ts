import {api} from "$lib/api/client";

export async function getHealth() {
    const response = await api.get("/api/health");
    return response.data;
}
