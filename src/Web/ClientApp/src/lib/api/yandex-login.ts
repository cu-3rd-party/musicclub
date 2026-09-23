import { api } from "$lib/api/client";

export type YandexLoginDto = {
    userId: string;
    yandexLogin: string | null;
    hasYandexLogin: boolean;
};

export async function getYandexLogin(): Promise<YandexLoginDto> {
    const response = await api.get<YandexLoginDto>("/api/v1/yandex-login");
    return response.data;
}

export async function updateYandexLogin(
    yandexLogin: string | null,
): Promise<YandexLoginDto> {
    const response = await api.put<YandexLoginDto>("/api/v1/yandex-login", {
        yandexLogin,
    });
    return response.data;
}
