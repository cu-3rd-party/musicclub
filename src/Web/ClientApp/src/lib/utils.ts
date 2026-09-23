import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & {
    ref?: U | null;
};

export type WithoutChild<T> = T extends { child?: unknown }
    ? Omit<T, "child">
    : T;

export type WithoutChildrenOrChild<T> = Omit<WithoutChild<T>, "children">;

export function cn(...inputs: ClassValue[]) {
    return twMerge(clsx(inputs));
}

/**
 * Форматирует дату в русском формате: "25 сен 2026, 19:00"
 */
export function formatDateRU(dateString: string): string {
    const date = new Date(dateString);

    const day = date.getDate();
    const month = date.toLocaleString("ru", { month: "short" });
    const year = date.getFullYear();
    const hours = date.getHours().toString().padStart(2, "0");
    const minutes = date.getMinutes().toString().padStart(2, "0");

    return `${day} ${month} ${year}, ${hours}:${minutes}`;
}
