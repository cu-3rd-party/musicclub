export const Permission = {
    ParticipationEditOwn: "participation.edit_own",
    ParticipationEditAny: "participation.edit_any",
    SongsEditOwn: "songs.edit_own",
    SongsEditAny: "songs.edit_any",
    SongsEditFeatured: "songs.edit_featured",
    EventsEdit: "events.edit",
    TracklistsEdit: "tracklists.edit",
} as const;

export const Roles = {
    Administrator: "Administrator",
    Roadie: "Roadie",
    Default: "Default",
} as const;

export const PermissionBundles = {
    Default: [Permission.ParticipationEditOwn, Permission.SongsEditOwn],
    Roadie: [
        Permission.ParticipationEditOwn,
        Permission.ParticipationEditAny,
        Permission.SongsEditOwn,
    ],
    All: [
        Permission.ParticipationEditOwn,
        Permission.ParticipationEditAny,
        Permission.SongsEditOwn,
        Permission.SongsEditAny,
        Permission.SongsEditFeatured,
        Permission.EventsEdit,
        Permission.TracklistsEdit,
    ],
} as const;

export enum PermissionCategory {
    None = "Нулевые",
    Basic = "Базовые",
    Roadie = "Роуди", // Added specific category for Roadie role
    Full = "Полные",
    Custom = "Кастомные",
}

const arePermissionSetsEqual = (
    a: readonly string[],
    b: readonly string[],
): boolean => {
    if (a.length !== b.length) return false;

    const setA = new Set(a);
    const setB = new Set(b);

    if (setA.size !== setB.size) return false;

    for (const item of setA) {
        if (!setB.has(item)) return false;
    }

    return true;
};

export function categorizePermissions(
    permissions: string[],
): PermissionCategory {
    // Case: No permissions
    if (permissions.length === 0) {
        return PermissionCategory.None;
    }

    // Case: Matches 'All' bundle (Administrator)
    if (arePermissionSetsEqual(permissions, PermissionBundles.All)) {
        return PermissionCategory.Full;
    }

    // Case: Matches 'Default' bundle
    if (arePermissionSetsEqual(permissions, PermissionBundles.Default)) {
        return PermissionCategory.Basic;
    }

    // Case: Matches 'Roadie' bundle
    if (arePermissionSetsEqual(permissions, PermissionBundles.Roadie)) {
        return PermissionCategory.Roadie;
    }

    // Case: Anything else (Mixed or modified permissions)
    return PermissionCategory.Custom;
}
