<script lang="ts">
    import {Plus} from "@lucide/svelte";
    import {Button, buttonVariants} from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog";
    import type {WithElementRef} from "$lib/utils";
    import type {HTMLFormAttributes} from "svelte/elements";
    import type {CreateSongPayload, Song} from "$lib/songs/types";
    import {createSong} from "$lib/api/songs";
    import SongForm from "./song-form.svelte";

    let {
        ref = $bindable(null),
        class: className,
        songsArray = $bindable(null),
        ...restProps
    }: WithElementRef<HTMLFormAttributes> & {
        songsArray: Song[] | null,
    } = $props();

    let dialogOpen = $state(false);

    async function handleSubmit(payload: CreateSongPayload) {
        const created = await createSong(payload);

        if (songsArray != null) {
            songsArray.push(created);
        }

        dialogOpen = false;
    }
</script>

<Dialog.Root bind:open={dialogOpen}>
    <Dialog.Trigger
        type="button"
        class="{className} {buttonVariants({
            size: 'icon',
            variant: 'default',
            className: 'rounded-full'
        })}"
        aria-label="Добавить песню"
    >
        <Plus/>
    </Dialog.Trigger>

    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Создать песню</Dialog.Title>
        </Dialog.Header>

        <SongForm
            submitLabel="Создать"
            submittingLabel="Создание..."
            onsubmit={handleSubmit}
        />
    </Dialog.Content>
</Dialog.Root>
