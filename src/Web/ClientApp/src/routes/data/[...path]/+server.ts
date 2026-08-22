import { API_URL } from "$lib/config";
import type { RequestHandler } from "./$types";

// может быть это добавляет х2 к лейтенси и к загрузке сети, но как будто похуй
export const GET: RequestHandler = async ({ params, url, fetch }) => {
    const target = new URL(`/api/v1/data/${params.path}${url.search}`, API_URL);

    const response = await fetch(target);

    return new Response(response.body, {
        status: response.status,
        headers: response.headers,
    });
};
