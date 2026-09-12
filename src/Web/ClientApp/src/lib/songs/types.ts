import type { UUID } from "node:crypto";

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
    roadie: SongUser | null;
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
    thumbnailDataEntryId: UUID | null;
    featured: boolean;
    availableRoles: string[] | null;
};

export type RolePayload = {
    actorUserId: UUID;
};

export type RoleCandidates = {
    users: SongUser[];
};

export type ListSongsParams = {
    query?: string;
    pageSize?: number;
    pageToken?: string;
};

export type ShortSongDto = {
    id: UUID;
    title: string;
    artist: string;
    thumbnailUrl: string | null;
};

export type SongRoleAssignment = {
    id: UUID;
    song: ShortSongDto;
    roleTitle: string;
    joinedAt: string;
};
