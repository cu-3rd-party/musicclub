import axios from "axios";
import { env } from "$env/dynamic/private";

export const ssr_api = axios.create({
    baseURL: env.API_SSR_URL?.trim() || "",
    headers: {
        "Content-Type": "application/json",
    },
});
