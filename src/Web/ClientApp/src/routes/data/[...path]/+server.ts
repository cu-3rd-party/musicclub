import { env } from "$env/dynamic/private";
import type { RequestHandler } from "./$types";

// может быть это добавляет х2 к лейтенси и к загрузке сети, но как будто похуй
export const GET: RequestHandler = async ({ params, url, fetch }) => {
    const target = new URL(`/api/v1/data/${params.path}${url.search}`, env.API_SSR_URL);

    const response = await fetch(target);

    return new Response(response.body, {
        status: response.status,
        headers: response.headers,
    });
};
