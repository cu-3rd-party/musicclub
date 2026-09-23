import { api } from "$lib/api/client";
import type { ShortSongDto, SongRoleAssignment } from "$lib/songs/types";

export type PrivacyPreferences = {
    allowAdding: boolean;
    allowRemoving: boolean;
};

export async function getPrivacyPreferences(): Promise<PrivacyPreferences> {
    const response = await api.get<PrivacyPreferences>(
        "/api/v1/users/me/preferences",
    );
    return response.data;
}

export async function updatePrivacyPreferences(
    payload: PrivacyPreferences,
): Promise<PrivacyPreferences> {
    const response = await api.put<PrivacyPreferences>(
        "/api/v1/users/me/preferences",
        payload,
    );
    return response.data;
}

export async function getMyAssignments(): Promise<SongRoleAssignment[]> {
    const response = await api.get<SongRoleAssignment[]>(
        "/api/v1/users/me/assignments",
    );
    return response.data;
}

export async function getMyRoadieAssignments(): Promise<ShortSongDto[]> {
    const response = await api.get<ShortSongDto[]>(
        "/api/v1/users/me/roadie/assignments",
    );
    return response.data;
}
