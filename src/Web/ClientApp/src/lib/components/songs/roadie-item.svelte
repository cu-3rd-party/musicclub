<script lang="ts">
    import {User} from "@lucide/svelte";
    import * as Avatar from "$lib/components/ui/avatar";
    import {Badge} from "$lib/components/ui/badge";
    import * as Command from "$lib/components/ui/command";
    import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
    import {Skeleton} from "$lib/components/ui/skeleton";
    import {assignRoadie, getRoadieCandidates, removeRoadie} from "$lib/api/songs";
    import type {Song, SongUser} from "$lib/songs/types";
    import {Permission} from "$lib/permissions/resolve";
    import type {UUID} from "node:crypto";

    let {
        songId,
        roadie,
        currentUser,
        onupdated,
    }: {
        songId: UUID;
        roadie: SongUser | null;
        currentUser: {id: UUID; permissions: string[]} | null;
        onupdated?: (song: Song) => void;
    } = $props();

    let assignOpen = $state(false);
    let query = $state("");
    let candidates = $state<SongUser[]>([]);
    let loadingCandidates = $state(false);
    let acting = $state(false);

    const isVacant = $derived(roadie === null);
    const isYou = $derived(roadie?.id === currentUser?.id);
    const canManage = $derived(
        currentUser !== null &&
        (currentUser.permissions ?? []).includes(Permission.RoadieManage)
    );

    // При открытии дропдауна и при изменении поиска (с дебаунсом) грузим кандидатов.
    $effect(() => {
        if (!assignOpen || !isVacant || !canManage) return;

        const handle = setTimeout(() => {
            loadCandidates();
        }, 150);

        return () => clearTimeout(handle);
    });

    async function loadCandidates() {
        loadingCandidates = true;
        try {
            const result = await getRoadieCandidates(
                songId,
                query || undefined,
            );
            candidates = result.users;
        } finally {
            loadingCandidates = false;
        }
    }

    async function assign(user: SongUser) {
        if (acting) return;
        acting = true;
        try {
            const updated = await assignRoadie(songId, {actorUserId: user.id});
            onupdated?.(updated);
            assignOpen = false;
        } finally {
            acting = false;
        }
    }

    async function remove() {
        if (acting) return;
        acting = true;
        try {
            const updated = await removeRoadie(songId);
            onupdated?.(updated);
        } finally {
            acting = false;
        }
    }

    function getInitials(name: string): string {
        return name
            .split(" ")
            .map((part) => part[0])
            .join("")
            .toUpperCase()
            .slice(0, 2);
    }
</script>

{#if isVacant}
    {#if canManage}
        <DropdownMenu.Root
            bind:open={assignOpen}
            onOpenChange={(open) => {
                if (open) {
                    query = "";
                    candidates = [];
                }
            }}
        >
            <DropdownMenu.Trigger
                class="flex w-full items-center justify-between p-3 rounded-lg bg-muted/50 text-left transition-colors hover:bg-muted cursor-pointer disabled:opacity-60"
                disabled={acting}
                aria-label="Назначить роуди"
            >
                <span class="flex items-center gap-3">
                    <Avatar.Root class="size-8">
                        <Avatar.Fallback class="text-xs bg-muted">
                            <User class="size-4"/>
                        </Avatar.Fallback>
                    </Avatar.Root>
                    <div>
                        <p class="text-sm font-medium">Роуди</p>
                        <p class="text-xs text-muted-foreground">Свободно</p>
                    </div>
                </span>
                <Badge variant="ghost">нажми чтоб зайти</Badge>
            </DropdownMenu.Trigger>

            <DropdownMenu.Content class="w-72 p-1">
                <Command.Root>
                    <Command.Input bind:value={query} placeholder="Поиск роуди..."/>
                    <Command.List>
                        {#if loadingCandidates}
                            {#each [0, 1, 2] as item (item)}
                                <div
                                    class="flex items-center gap-2 px-2 py-1.5"
                                >
                                    <Skeleton class="size-6 rounded-full"/>
                                    <Skeleton class="h-4 w-24"/>
                                </div>
                            {/each}
                        {:else if candidates.length === 0}
                            <Command.Empty>Никого нельзя назначить</Command.Empty>
                        {:else}
                            {#each candidates as user (user.id)}
                                <Command.Item
                                    value={user.displayName}
                                    onSelect={() => assign(user)}
                                    disabled={acting}
                                >
                                    <Avatar.Root class="size-6">
                                        <Avatar.Image
                                            src={user.avatarUrl}
                                            alt={user.displayName}
                                        />
                                        <Avatar.Fallback class="text-xs">
                                            {getInitials(user.displayName)}
                                        </Avatar.Fallback>
                                    </Avatar.Root>
                                    <span class="truncate">{user.displayName}</span>
                                    {#if user.id === currentUser?.id}
                                        <Badge
                                            variant="ghost"
                                            class="ml-auto shrink-0"
                                        >
                                            вы
                                        </Badge>
                                    {/if}
                                </Command.Item>
                            {/each}
                        {/if}
                    </Command.List>
                </Command.Root>
            </DropdownMenu.Content>
        </DropdownMenu.Root>
    {:else}
        <div
            class="flex w-full items-center justify-between p-3 rounded-lg bg-muted/50 text-left cursor-default"
        >
            <span class="flex items-center gap-3">
                <Avatar.Root class="size-8">
                    <Avatar.Fallback class="text-xs bg-muted">
                        <User class="size-4"/>
                    </Avatar.Fallback>
                </Avatar.Root>
                <div>
                    <p class="text-sm font-medium">Роуди</p>
                    <p class="text-xs text-muted-foreground">Свободно</p>
                </div>
            </span>
            <Badge variant="ghost">свободно</Badge>
        </div>
    {/if}
{:else if roadie}
    {#if canManage}
        <button
            type="button"
            class="flex w-full items-center justify-between p-3 rounded-lg bg-muted/50 text-left transition-colors hover:bg-muted cursor-pointer disabled:opacity-60"
            onclick={remove}
            disabled={acting}
            title={isYou ? "Нажми чтоб снять себя с роли" : "Нажми чтоб снять с роли"}
            aria-label="Снять роуди с песни"
        >
            <span class="flex items-center gap-3">
                <Avatar.Root class="size-8">
                    <Avatar.Image
                        src={roadie.avatarUrl}
                        alt={roadie.displayName}
                    />
                    <Avatar.Fallback class="text-xs">
                        {getInitials(roadie.displayName)}
                    </Avatar.Fallback>
                </Avatar.Root>
                <div>
                    <p class="text-sm font-medium">Роуди</p>
                    <p class="text-xs text-muted-foreground">{roadie.displayName}</p>
                </div>
            </span>
            <Badge variant="ghost">нажми чтоб снять</Badge>
        </button>
    {:else}
        <div
            class="flex w-full items-center justify-between p-3 rounded-lg bg-muted/50 text-left cursor-default"
        >
            <span class="flex items-center gap-3">
                <Avatar.Root class="size-8">
                    <Avatar.Image
                        src={roadie.avatarUrl}
                        alt={roadie.displayName}
                    />
                    <Avatar.Fallback class="text-xs">
                        {getInitials(roadie.displayName)}
                    </Avatar.Fallback>
                </Avatar.Root>
                <div>
                    <p class="text-sm font-medium">Роуди</p>
                    <p class="text-xs text-muted-foreground">{roadie.displayName}</p>
                </div>
            </span>
            <Badge variant="default">занято</Badge>
        </div>
    {/if}
{/if}