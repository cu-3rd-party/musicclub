import type { UserProfile } from './auth.models';

export type { UserProfile } from './auth.models';

export type SongLinkKind = 'external' | 'youtube' | 'spotify' | 'soundcloud' | 'bandcamp';

export interface SongThumbnail {
    url: string;
    color: string;
}

export interface Song {
    id: string;
    title: string;
    artist: string;
    linkKind: SongLinkKind;
    linkUrl?: string;
    isFeatured: boolean;
    thumbnail?: SongThumbnail;
    createdAt: string;
    updatedAt: string;
    createdBy: string;
    roles?: SongRole[];
    roadie?: RoadieAssignment;
}

export interface CreateSongPayload {
    title: string;
    artist: string;
    linkKind: SongLinkKind;
    linkUrl?: string;
    isFeatured?: boolean;
}

export interface UpdateSongPayload extends Partial<CreateSongPayload> {}

export interface ListSongsParams {
    page?: number;
    pageSize?: number;
    search?: string;
    featured?: boolean;
}

export interface ListSongsResult {
    items: Song[];
    total: number;
    page: number;
    pageSize: number;
}

export interface SongRole {
    id: string;
    name: string;
    songId: string;
    assignments: RoleAssignment[];
}

export interface RoleAssignment {
    id: string;
    roleId: string;
    userId: string;
    userDisplayName: string;
    userAvatarUrl?: string;
}

export interface RolePayload {
    userId: string;
}

export interface RoleCandidates {
    candidates: UserProfile[];
    assigned: UserProfile[];
}

export interface RoadieAssignment {
    userId: string;
    userDisplayName: string;
    userAvatarUrl?: string;
    assignedAt: string;
}
