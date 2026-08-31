<script lang="ts">
    import {Pencil} from "@lucide/svelte";
    import {buttonVariants} from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog";
    import {updateSong} from "$lib/api/songs";
    import type {Song, UpdateSongPayload} from "$lib/songs/types";
    import {cn} from "$lib/utils";
    import SongForm from "./song-form.svelte";

    let {
        song,
        onupdated,
    }: {
        song: Song;
        onupdated?: (song: Song) => void;
    } = $props();

    let dialogOpen = $state(false);
</script>

<Dialog.Root bind:open={dialogOpen}>
    <Dialog.Trigger
        type="button"
        class={cn(buttonVariants({ variant: 'ghost', size: 'icon' }))}
        aria-label="Редактировать песню"
    >
        <Pencil class="size-5"/>
    </Dialog.Trigger>

    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Редактировать песню</Dialog.Title>
        </Dialog.Header>

        <SongForm
            bind:title={song.title}
            bind:artist={song.artist}
            bind:description={song.description}
            bind:url={song.url}
            bind:featured={song.featured}
            bind:thumbnailUrl={song.thumbnailUrl}
            availableRoles={song.roles.map((r) => r.title)}
            submitLabel="Сохранить"
            submittingLabel="Сохранение..."
            onsubmit={async (payload) => {
                const updated = await updateSong(song.id, payload as UpdateSongPayload);
                onupdated?.(updated);
                dialogOpen = false;
            }}
        />
    </Dialog.Content>
</Dialog.Root>
