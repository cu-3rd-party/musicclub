<script lang="ts">
    import {Music, Star} from "@lucide/svelte";
    import {Badge} from "$lib/components/ui/badge";
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";

    let {
        songId,
        title,
        artist,
        featured = false,
        imageUrl = null,
        filledAssignments = 0,
        totalAssignments = 0,
        roleTitle = null,
    }: {
        songId: string,
        title: string,
        artist: string,
        featured?: boolean,
        imageUrl?: string | null,
        filledAssignments?: number,
        totalAssignments?: number,
        roleTitle?: string | null,
    } = $props();

    async function navigateToSong() {
        await goto(resolve(`/app/songs/${songId}`));
    }
</script>

<button
    type="button"
    class="group flex w-full items-center gap-3 py-2 text-left transition-colors hover:bg-muted/50 rounded-md cursor-pointer"
    onclick={navigateToSong}
>
    <div class="relative size-10 shrink-0 overflow-hidden rounded-full">
        {#if imageUrl}
            <img
                src={imageUrl}
                alt={title}
                class="h-full w-full object-cover"
            />
        {:else}
            <div class="h-full w-full bg-muted flex items-center justify-center">
                <Music class="size-4 text-muted-foreground"/>
            </div>
        {/if}
    </div>

    <div class="flex-1 min-w-0 flex items-center gap-2">
        <span class="truncate font-medium text-sm">
            {title}
        </span>

        {#if !roleTitle}
            <span class="text-muted-foreground text-xs">—</span>

            <span class="truncate text-muted-foreground text-sm">
                {artist}
            </span>
        {/if}

        {#if featured}
            <Star class="size-3.5 shrink-0 text-yellow-500 fill-yellow-500"/>
        {/if}
    </div>

    {#if roleTitle}
        <Badge variant="outline" class="shrink-0 text-xs">
            {roleTitle}
        </Badge>
    {:else if totalAssignments !== 0}
        <Badge variant="secondary" class="shrink-0 text-xs">
            {filledAssignments}/{totalAssignments}
        </Badge>
    {/if}
</button>
