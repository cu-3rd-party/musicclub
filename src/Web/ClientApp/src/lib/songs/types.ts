import type {UUID} from "node:crypto";

export type SongUser = {
    id: UUID;
    displayName: string;
    username: string | null;
    avatarUrl: string | null;
};

export type RoleAssignment = {
    id: UUID;
    user: SongUser;
    joinedAt: string;
};

export type SongRole = {
    id: UUID;
    title: string;
    assignment: RoleAssignment | null;
};

export type Song = {
    id: UUID;
    title: string;
    artist: string;
    description: string | null;
    url: string;
    thumbnailUrl: string | null;
    featured: boolean;
    createdBy: SongUser;
    roles: SongRole[];
    createdAt: string;
    updatedAt: string;
};

export type ListSongsResult = {
    songs: Song[];
    nextPageToken: string | null;
};

export type CreateSongPayload = {
    title: string;
    artist: string;
    description: string | null;
    url: string;
    thumbnailDataEntryId: UUID | null;
    featured: boolean;
    availableRoles: string[] | null;
};

export type UpdateSongPayload = {
    title: string;
    artist: string;
    description: string | null;
    url: string;
    thumbnailUrl: string | null;
    featured: boolean;
    availableRoles: string[] | null;
};

export type RolePayload = {
    actorUserId: UUID;
};

export type ListSongsParams = {
    query?: string;
    pageSize?: number;
    pageToken?: string;
};
