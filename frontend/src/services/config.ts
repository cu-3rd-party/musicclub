import { createConnectTransport } from "@connectrpc/connect-web";

export const BACKEND_URL = import.meta.env.BASE_URL ?? "http://backend:6969";
export const transport = createConnectTransport({
	baseUrl: BACKEND_URL,
});

var TOKEN = "PLACEHOLDER";

export function getToken(): string {
	return TOKEN;
}

export function setToken(token: string) {
	TOKEN = token;
}
