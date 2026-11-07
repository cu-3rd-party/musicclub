import axios from "axios";

/** Тело ошибки бэкенда: ProblemDetails (RFC 9457), для 400 — с errors по полям. */
type ApiErrorPayload = {
    title?: string;
    detail?: string;
    // ValidationProblemDetails: { errors: { field: ["сообщение"] } }
    errors?: Record<string, string[]>;
    message?: string;
};

/** Текст ошибки для пользователя: detail от бэкенда, иначе понятное описание по статусу, иначе fallbackMessage. */
export function getApiErrorMessage(
    error: unknown,
    fallbackMessage: string,
): string {
    if (!axios.isAxiosError<ApiErrorPayload | string>(error)) {
        return fallbackMessage;
    }

    const response = error.response;
    if (!response) {
        return "Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.";
    }

    const data = response.data;
    if (typeof data === "string") {
        // Старые эндпоинты и прокси отдают текст; HTML-страницу ошибки показывать не стоит
        return data && !data.trimStart().startsWith("<") ? data : fallbackMessage;
    }

    const validationMessage = data?.errors
        ? Object.values(data.errors).flat()[0]
        : undefined;
    const message = data?.detail ?? validationMessage ?? data?.message;
    if (message) {
        return message;
    }

    switch (response.status) {
        case 401:
            return "Сессия истекла — войдите ещё раз.";
        case 403:
            return "У вас нет прав на это действие.";
        case 404:
            return "Не найдено — возможно, это уже удалили.";
        case 413:
            return "Файл слишком большой.";
        default:
            return response.status >= 500
                ? `${fallbackMessage}: ошибка сервера, попробуйте позже.`
                : fallbackMessage;
    }
}
