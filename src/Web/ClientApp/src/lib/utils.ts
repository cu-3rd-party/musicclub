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
