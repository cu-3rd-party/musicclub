<script lang="ts">
    import {LogOut, User} from "@lucide/svelte";
    import * as Avatar from "$lib/components/ui/avatar";
    import {Badge} from "$lib/components/ui/badge";
    import {Button} from "$lib/components/ui/button";
    import * as Command from "$lib/components/ui/command";
    import * as Popover from "$lib/components/ui/popover";
    import {Skeleton} from "$lib/components/ui/skeleton";
    import {getRoleCandidates, joinSongRole, leaveSongRole} from "$lib/api/songs";
    import type {Song, SongRole, SongUser} from "$lib/songs/types";
    import {Permission} from "$lib/permissions/resolve";
    import type {UUID} from "node:crypto";

    let {
        songId,
        role,
        currentUser,
        removableUserIds,
        onupdated,
    }: {
        songId: UUID;
        role: SongRole;
        currentUser: {id: UUID; permissions: string[]} | null;
        removableUserIds: Set<string>;
        onupdated?: (song: Song) => void;
    } = $props();

    let assignOpen = $state(false);
    let query = $state("");
    let candidates = $state<SongUser[]>([]);
    let loadingCandidates = $state(false);
    let acting = $state(false);

    const isVacant = $derived(role.assignment === null);
    const member = $derived(role.assignment?.user ?? null);
    const isYou = $derived(member?.id === currentUser?.id);
    const canAssign = $derived(
        currentUser !== null &&
        ((currentUser.permissions ?? []).includes(Permission.ParticipationEditOwn) ||
            (currentUser.permissions ?? []).includes(Permission.ParticipationEditAny))
    );
    const canRemoveMember = $derived(
        member !== null && currentUser !== null && removableUserIds.has(member.id)
    );

    // При открытии поповера и при изменении поиска (с дебаунсом) грузим кандидатов.
    $effect(() => {
        if (!assignOpen || !isVacant || !canAssign) return;

        const handle = setTimeout(() => {
            loadCandidates();
        }, 150);

        return () => clearTimeout(handle);
    });

    async function loadCandidates() {
        loadingCandidates = true;
        try {
            const result = await getRoleCandidates(
                songId,
                role.id,
                query || undefined,
                "assign",
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
            const updated = await joinSongRole(role.id, {actorUserId: user.id});
            onupdated?.(updated);
            assignOpen = false;
        } finally {
            acting = false;
        }
    }

    async function remove() {
        if (!member || acting) return;
        acting = true;
        try {
            const updated = await leaveSongRole(role.id, {actorUserId: member.id});
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
    {#if canAssign}
        <Popover.Root bind:open={assignOpen}>
            <Popover.Trigger
                class="flex w-full items-center justify-between gap-3 rounded-lg bg-muted/50 p-3 text-left transition-colors hover:bg-muted cursor-pointer disabled:opacity-60"
                disabled={acting}
                aria-label={`Назначить на роль ${role.title}`}
            >
                <span class="flex items-center gap-3">
                    <Avatar.Root class="size-8">
                        <Avatar.Fallback class="text-xs bg-muted">
                            <User class="size-4"/>
                        </Avatar.Fallback>
                    </Avatar.Root>
                    <div>
                        <p class="text-sm font-medium">{role.title}</p>
                        <p class="text-xs text-muted-foreground">Свободно</p>
                    </div>
                </span>
                <Badge variant="ghost">Назначить</Badge>
            </Popover.Trigger>

            <Popover.Content align="end" class="w-72">
                <Popover.Header>
                    <Popover.Title>{role.title}</Popover.Title>
                    <Popover.Description>Кого назначить на роль</Popover.Description>
                </Popover.Header>

                <Command.Root>
                    {#if loadingCandidates && query === ""}
                        <div class="space-y-1 px-1 pb-1">
                            <Skeleton class="h-8 w-full rounded-md"/>
                            <div class="space-y-1">
                                {#each [0, 1, 2, 3] as item (item)}
                                    <div
                                        class="flex items-center gap-2 px-2 py-1.5"
                                    >
                                        <Skeleton class="size-6 rounded-full"/>
                                        <Skeleton class="h-4 w-24"/>
                                    </div>
                                {/each}
                            </div>
                        </div>
                    {:else}
                        <Command.Input bind:value={query} placeholder="Поиск участника..."/>
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
                    {/if}
                </Command.Root>
            </Popover.Content>
        </Popover.Root>
    {:else}
        <div
            class="flex w-full items-center justify-between gap-3 rounded-lg bg-muted/50 p-3 text-left cursor-default"
        >
            <span class="flex items-center gap-3">
                <Avatar.Root class="size-8">
                    <Avatar.Fallback class="text-xs bg-muted">
                        <User class="size-4"/>
                    </Avatar.Fallback>
                </Avatar.Root>
                <div>
                    <p class="text-sm font-medium">{role.title}</p>
                    <p class="text-xs text-muted-foreground">Свободно</p>
                </div>
            </span>
            <Badge variant="ghost">Свободно</Badge>
        </div>
    {/if}
{:else if member}
    {#if canRemoveMember}
        <Popover.Root>
            <Popover.Trigger
                class="flex w-full items-center justify-between gap-3 rounded-lg bg-muted/50 p-3 text-left transition-colors hover:bg-muted cursor-pointer disabled:opacity-60"
                disabled={acting}
                aria-label={`Управление ролью ${role.title}`}
            >
                <span class="flex items-center gap-3">
                    <Avatar.Root class="size-8">
                        <Avatar.Image
                            src={member.avatarUrl}
                            alt={member.displayName}
                        />
                        <Avatar.Fallback class="text-xs">
                            {getInitials(member.displayName)}
                        </Avatar.Fallback>
                    </Avatar.Root>
                    <div>
                        <p class="text-sm font-medium">{role.title}</p>
                        <p class="text-xs text-muted-foreground">{member.displayName}</p>
                    </div>
                </span>
                <Badge variant={isYou ? "ghost" : "default"}>
                    {isYou ? "Это вы" : "Занято"}
                </Badge>
            </Popover.Trigger>

            <Popover.Content align="end" class="w-64">
                <Popover.Header class="flex flex-row items-center gap-3">
                    <Avatar.Root class="size-9">
                        <Avatar.Image
                            src={member.avatarUrl}
                            alt={member.displayName}
                        />
                        <Avatar.Fallback>
                            {getInitials(member.displayName)}
                        </Avatar.Fallback>
                    </Avatar.Root>
                    <div class="flex flex-col gap-0.5">
                        <Popover.Title>{role.title}</Popover.Title>
                        <Popover.Description>{member.displayName}</Popover.Description>
                    </div>
                </Popover.Header>

                <Button
                    variant="destructive"
                    class="w-full"
                    onclick={remove}
                    disabled={acting}
                >
                    <LogOut/>
                    {isYou ? "Выйти" : "Снять с роли"}
                </Button>
            </Popover.Content>
        </Popover.Root>
    {:else}
        <div
            class="flex w-full items-center justify-between gap-3 rounded-lg bg-muted/50 p-3 text-left cursor-default"
        >
            <span class="flex items-center gap-3">
                <Avatar.Root class="size-8">
                    <Avatar.Image
                        src={member.avatarUrl}
                        alt={member.displayName}
                    />
                    <Avatar.Fallback class="text-xs">
                        {getInitials(member.displayName)}
                    </Avatar.Fallback>
                </Avatar.Root>
                <div>
                    <p class="text-sm font-medium">{role.title}</p>
                    <p class="text-xs text-muted-foreground">{member.displayName}</p>
                </div>
            </span>
            <Badge variant="default">Занято</Badge>
        </div>
    {/if}
{/if}