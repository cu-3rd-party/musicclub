import axios, { AxiosHeaders } from "axios";

import { getStoredAuthSession } from "$lib/auth/storage";
import {API_URL} from "$lib/config";

export const api = axios.create({
    baseURL: API_URL,
    headers: {
        "Content-Type": "application/json",
    },
});

api.interceptors.request.use((config) => {
    config.headers = AxiosHeaders.from(config.headers);

    if (typeof FormData !== "undefined" && config.data instanceof FormData) {
        config.headers.delete("Content-Type");
    }

    const session = getStoredAuthSession();
    if (session?.accessToken) {
        config.headers.set("Authorization", `Bearer ${session.accessToken}`);
    }

    return config;
});
