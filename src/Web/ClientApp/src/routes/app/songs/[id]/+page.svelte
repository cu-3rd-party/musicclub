<script lang="ts">
    import {page} from "$app/state";
    import {Button} from "$lib/components/ui/button";
    import {Badge} from "$lib/components/ui/badge";
    import * as Avatar from "$lib/components/ui/avatar";
    import {Separator} from "$lib/components/ui/separator";
    import {Skeleton} from "$lib/components/ui/skeleton";
    import {getSong} from "$lib/api/songs";
    import type {Song} from "$lib/songs/types";
    import {
        ArrowLeft,
        ExternalLink,
        Star,
        User,
        Music,
    } from "@lucide/svelte";

    let song = $state<Song | null>(null);
    let loading = $state(true);
    let error = $state<string | null>(null);

    const songId = $derived(page.params.id as string);

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

    function handleBack() {
        history.back();
    }

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
                onclick={handleBack}
                aria-label="Назад"
            >
                <ArrowLeft class="size-5"/>
            </Button>
            <!--{#if loading}-->
            <!--    <Skeleton class="h-6 w-48"/>-->
            <!--{:else if song}-->
            <!--    <h1 class="text-lg font-semibold truncate">{song.title}</h1>-->
            <!--{/if}-->
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
                        {#each {length: 3} as _}
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
                <div class="relative aspect-video w-full">
                    <img
                        src={song.thumbnailUrl}
                        alt={song.title}
                        class="h-full w-full object-cover"
                    />
                    {#if song.featured}
                        <Badge
                            class="absolute top-3 right-3 bg-yellow-500 text-white border-0"
                        >
                            <Star class="size-3 mr-1"/>
                            Избранное
                        </Badge>
                    {/if}
                </div>
            {:else}
                <div class="relative aspect-video w-full bg-muted flex items-center justify-center">
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

                <Button
                    variant="outline"
                    size="sm"
                    onclick={() => song && window.open(song.url, "_blank")}
                >
                    <ExternalLink class="size-4 mr-2"/>
                    Открыть ссылку
                </Button>

                <Separator/>

                <div>
                    <h3 class="text-sm font-semibold mb-3 uppercase tracking-wide text-muted-foreground">
                        Роли
                    </h3>
                    <div class="space-y-2">
                        {#each song.roles as role}
                            <div
                                class="flex items-center justify-between p-3 rounded-lg bg-muted/50"
                            >
                                <div class="flex items-center gap-3">
                                    {#if role.assignment}
                                        <Avatar.Root class="size-8">
                                            <Avatar.Image
                                                src={role.assignment.user.avatarUrl}
                                                alt={role.assignment.user.displayName}
                                            />
                                            <Avatar.Fallback class="text-xs">
                                                {getInitials(role.assignment.user.displayName)}
                                            </Avatar.Fallback>
                                        </Avatar.Root>
                                        <div>
                                            <p class="text-sm font-medium">
                                                {role.title}
                                            </p>
                                            <p class="text-xs text-muted-foreground">
                                                {role.assignment.user.displayName}
                                            </p>
                                        </div>
                                    {:else}
                                        <Avatar.Root class="size-8">
                                            <Avatar.Fallback class="text-xs bg-muted">
                                                <User class="size-4"/>
                                            </Avatar.Fallback>
                                        </Avatar.Root>
                                        <div>
                                            <p class="text-sm font-medium">
                                                {role.title}
                                            </p>
                                            <p class="text-xs text-muted-foreground">
                                                Свободно
                                            </p>
                                        </div>
                                    {/if}
                                </div>

                                <Badge variant={role.assignment ? "default" : "secondary"}>
                                    {role.assignment ? "Занято" : "Свободно"}
                                </Badge>
                            </div>
                        {/each}
                    </div>
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
