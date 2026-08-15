import {api} from "$lib/api/client";

import type {
    CreateSongPayload,
    ListSongsParams,
    ListSongsResult,
    RolePayload,
    Song,
    UpdateSongPayload,
} from "$lib/songs/types";
import type {UUID} from "node:crypto";

export async function getSongs(
    params?: ListSongsParams,
): Promise<ListSongsResult> {
    const response = await api.get<ListSongsResult>(
        "/api/v1/songs",
        {params},
    );

    return response.data;
}

export async function createSong(
    payload: CreateSongPayload,
): Promise<Song> {
    const response = await api.post<Song>(
        "/api/v1/songs",
        payload,
    );

    return response.data;
}

export async function getSong(
    songId: UUID,
): Promise<Song> {
    const response = await api.get<Song>(
        `/api/v1/songs/${songId}`,
    );

    return response.data;
}

export async function updateSong(
    songId: UUID,
    payload: UpdateSongPayload,
): Promise<Song> {
    const response = await api.put<Song>(
        `/api/v1/songs/${songId}`,
        payload,
    );

    return response.data;
}

export async function deleteSong(
    songId: UUID,
): Promise<void> {
    await api.delete(
        `/api/v1/songs/${songId}`,
    );
}

export async function joinSongRole(
    roleId: UUID,
    payload?: RolePayload,
): Promise<Song> {
    const response = await api.post<Song>(
        `/api/v1/songs/roles/${roleId}/join`,
        payload,
    );

    return response.data;
}

export async function leaveSongRole(
    roleId: UUID,
    payload?: RolePayload,
): Promise<Song> {
    const response = await api.post<Song>(
        `/api/v1/songs/roles/${roleId}/leave`,
        payload,
    );

    return response.data;
}
