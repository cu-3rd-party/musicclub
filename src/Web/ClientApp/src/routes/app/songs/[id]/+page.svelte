<script lang="ts">
    import {page} from "$app/state";
    import {Button} from "$lib/components/ui/button";
    import {Badge} from "$lib/components/ui/badge";
    import * as Avatar from "$lib/components/ui/avatar";
    import {Separator} from "$lib/components/ui/separator";
    import {Skeleton} from "$lib/components/ui/skeleton";
    import {getSong, getRoleCandidates} from "$lib/api/songs";
    import type {Song} from "$lib/songs/types";
    import {getStoredAuthSession} from "$lib/auth/storage";
    import {ArrowLeft, ExternalLink, Music, Star,} from "@lucide/svelte";
    import type {UUID} from "node:crypto";
    import EditSong from "$lib/components/songs/edit-song.svelte";
    import RoleItem from "$lib/components/songs/role-item.svelte";
    import RoadieItem from "$lib/components/songs/roadie-item.svelte";

    let song = $state<Song | null>(null);
    let loading = $state(true);
    let error = $state<string | null>(null);
    let removableUserIds = $state<Set<string>>(new Set());
    let roadieError = $state<string | null>(null);

    const songId = $derived(page.params.id as UUID);
    const currentUser = $derived(getStoredAuthSession()?.user ?? null);

    async function loadRemovableMembers(song: Song) {
        const firstRoleId = song.roles[0]?.id;
        if (!firstRoleId) return;

        try {
            const result = await getRoleCandidates(
                song.id,
                firstRoleId,
                undefined,
                "remove",
            );
            removableUserIds = new Set(result.users.map((u) => u.id));
        } catch (err) {
            console.error(err);
        }
    }

    $effect(() => {
        const id = songId;

        let cancelled = false;

        async function loadSong() {
            loading = true;
            error = null;

            try {
                const result = await getSong(id);

                if (!cancelled) {
                    song = result;
                    await loadRemovableMembers(result);
                }
            } catch (err) {
                if (!cancelled) {
                    error = "Не удалось загрузить песню";
                    console.error(err);
                }
            } finally {
                if (!cancelled) {
                    loading = false;
                }
            }
        }

        loadSong();

        return () => {
            cancelled = true;
        };
    });

    function getInitials(name: string): string {
        return name
            .split(" ")
            .map((part) => part[0])
            .join("")
            .toUpperCase()
            .slice(0, 2);
    }

    function formatDate(dateString: string): string {
        return new Date(dateString).toLocaleDateString("ru-RU", {
            day: "numeric",
            month: "long",
            year: "numeric",
        });
    }
</script>

<main class="w-full h-full flex flex-col">
    <div class="sticky top-0 z-10 bg-background border-b border-border">
        <div class="flex items-center gap-2 px-4 py-3">
            <Button
                variant="ghost"
                size="icon"
                onclick={() => history.back()}
                aria-label="Назад"
            >
                <ArrowLeft class="size-5"/>
            </Button>

            {#if song}
                <div class="ml-auto">
                    <EditSong
                        {song}
                        onupdated={(updated) => (song = updated)}
                    />
                </div>
            {/if}
        </div>
    </div>

    <div class="flex-1 overflow-y-auto">
        {#if loading}
            <Skeleton class="aspect-video w-full rounded-none"/>
            <div class="p-4 space-y-4">
                <div class="space-y-2">
                    <Skeleton class="h-8 w-64"/>
                    <Skeleton class="h-5 w-40"/>
                </div>
                <Skeleton class="h-4 w-full"/>
                <Skeleton class="h-4 w-3/4"/>
                <Skeleton class="h-9 w-40"/>
                <Separator/>
                <div>
                    <Skeleton class="h-4 w-24 mb-3"/>
                    <div class="space-y-2">
                        {#each [0, 1, 2] as item (item)}
                            <div class="flex items-center justify-between p-3 rounded-lg bg-muted/50">
                                <div class="flex items-center gap-3">
                                    <Skeleton class="size-8 rounded-full"/>
                                    <div class="space-y-1">
                                        <Skeleton class="h-4 w-20"/>
                                        <Skeleton class="h-3 w-32"/>
                                    </div>
                                </div>
                                <Skeleton class="h-5 w-16"/>
                            </div>
                        {/each}
                    </div>
                </div>
                <Separator/>
                <div class="mt-2.5">
                    <div class="flex items-center justify-between p-3 rounded-lg bg-muted/50">
                        <div class="flex items-center gap-3">
                            <Skeleton class="size-8 rounded-full"/>
                            <div class="space-y-1">
                                <Skeleton class="h-4 w-16"/>
                                <Skeleton class="h-3 w-24"/>
                            </div>
                        </div>
                        <Skeleton class="h-5 w-16"/>
                    </div>
                </div>
                <Separator/>
                <div class="space-y-2">
                    <div class="flex items-center gap-3">
                        <Skeleton class="size-10 rounded-full"/>
                        <div class="space-y-1">
                            <Skeleton class="h-4 w-32"/>
                            <Skeleton class="h-3 w-24"/>
                        </div>
                    </div>
                    <Skeleton class="h-3 w-48"/>
                    <Skeleton class="h-3 w-40"/>
                </div>
            </div>
        {:else if error}
            <div class="py-8 text-center text-destructive">
                {error}
            </div>
        {:else if song}
        {#if song.thumbnailUrl}
            <div class="relative aspect-video w-full md:mx-auto md:max-w-3xl lg:max-w-4xl">
                <img
                    src={song.thumbnailUrl}
                    alt={song.title}
                    class="h-full w-full object-cover md:rounded-lg"
                />
                {#if song.featured}
                    <Star class="absolute top-2 right-2 size-6"/>
                {/if}
            </div>
        {:else}
            <div class="relative aspect-video w-full bg-muted flex items-center justify-center md:mx-auto md:max-w-3xl lg:max-w-4xl md:rounded-lg">
                <Music class="size-12 text-muted-foreground"/>
                {#if song.featured}
                    <Badge
                        class="absolute top-3 right-3 bg-yellow-500 text-white border-0"
                    >
                        <Star class="size-3 mr-1"/>
                        Избранное
                    </Badge>
                {/if}
            </div>
        {/if}

            <div class="p-4 space-y-4">
                <div>
                    <h2 class="text-2xl font-bold">{song.title}</h2>
                    <p class="text-lg text-muted-foreground">{song.artist}</p>
                </div>

                {#if song.description}
                    <p class="text-sm text-muted-foreground leading-relaxed">
                        {song.description}
                    </p>
                {/if}

                <div class="flex gap-2">
                    <Button
                        variant="outline"
                        size="sm"
                        onclick={() => song && window.open(song.url, "_blank")}
                    >
                        <ExternalLink class="size-4 mr-2"/>
                        Открыть ссылку
                    </Button>
                </div>

                {#if roadieError}
                    <p class="text-sm text-destructive">{roadieError}</p>
                {/if}

                <Separator/>

                <div>
                    <h3 class="text-sm font-semibold mb-3 uppercase tracking-wide text-muted-foreground">
                        Роли
                    </h3>
                    <div class="space-y-2">
                        {#each song.roles as role (role.id)}
                            <RoleItem
                                songId={song.id}
                                {role}
                                currentUser={currentUser}
                                removableUserIds={removableUserIds}
                                onupdated={(updated) => (song = updated)}
                            />
                        {/each}
                    </div>
                </div>

                <Separator/>

                <div>
                    <RoadieItem
                        songId={song.id}
                        roadie={song.roadie}
                        currentUser={currentUser}
                        onupdated={(updated) => (song = updated)}
                    />
                </div>

                <Separator/>

                <div class="space-y-2">
                    <div class="flex items-center gap-3">
                        <Avatar.Root class="size-10">
                            <Avatar.Image
                                src={song.createdBy.avatarUrl}
                                alt={song.createdBy.displayName}
                            />
                            <Avatar.Fallback>
                                {getInitials(song.createdBy.displayName)}
                            </Avatar.Fallback>
                        </Avatar.Root>
                        <div>
                            <p class="text-sm font-medium">
                                {song.createdBy.displayName}
                            </p>
                            <p class="text-xs text-muted-foreground">
                                Создал(а) песню
                            </p>
                        </div>
                    </div>

                    <div class="text-xs text-muted-foreground space-y-1">
                        <p>Создано: {formatDate(song.createdAt)}</p>
                        <p>Обновлено: {formatDate(song.updatedAt)}</p>
                    </div>
                </div>
            </div>
        {/if}
    </div>
</main>
