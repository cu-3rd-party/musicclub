<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group";
    import {
        ClipboardPaste,
        Link,
        MessageSquare,
        Music2,
        Plus,
        User
    } from "@lucide/svelte";
    import {Button, buttonVariants} from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog"
    import type {WithElementRef} from "$lib/utils";
    import type {HTMLFormAttributes} from "svelte/elements";
    import type {CreateSongPayload} from "$lib/songs/types";
    import {createSong} from "$lib/api/songs";

    let {
        ref = $bindable(null),
        class: className,
        payload = $bindable<CreateSongPayload>({
            title: "",
            artist: "",
            description: null,
            url: "",
            thumbnailUrl: null,
            featured: false,
            availableRoles: null,
        }),
        ...restProps
    }: WithElementRef<HTMLFormAttributes> & {
        payload?: CreateSongPayload
    } = $props();

    let dialogOpen = $state(false);

    async function handleSubmit(event: SubmitEvent) {
        event.preventDefault();

        await createSong({
            ...payload,
            title: payload.title.trim(),
            artist: payload.artist.trim(),
            url: payload.url.trim(),
            description: payload.description?.trim() || null,
        });

        dialogOpen = false;
    }

    async function pasteUrl() {
        payload.url = await navigator.clipboard.readText();
    }
</script>

<Dialog.Root bind:open={dialogOpen}>
    <Dialog.Trigger
        type="button"
        class="{className} {buttonVariants({size: 'icon', variant: 'default', className: 'rounded-full'})}"
        aria-label="Добавить песню"
    >
        <Plus/>
    </Dialog.Trigger>
    <Dialog.Content>
        <form onsubmit={handleSubmit} class="grid gap-4">
            <Dialog.Header>
                <Dialog.Title>Создать песню</Dialog.Title>
            </Dialog.Header>
<!--            for some reason there is no space between these 2-->
                <InputGroup.Root>
                    <InputGroup.Input
                        required={true}
                        placeholder="Название"
                        bind:value={payload.title}
                    />
                    <InputGroup.Addon>
                        <Music2/>
                    </InputGroup.Addon>
                </InputGroup.Root>
                <InputGroup.Root>
                    <InputGroup.Input
                        required={true}
                        placeholder="Исполнитель"
                        bind:value={payload.artist}
                    />
                    <InputGroup.Addon>
                        <User/>
                    </InputGroup.Addon>
                </InputGroup.Root>
                <InputGroup.Root>
                    <InputGroup.Textarea
                        placeholder="Добавь комментарий"
                        bind:value={payload.description}
                    />
                    <InputGroup.Addon align="block-end">
                        <MessageSquare/>
                    </InputGroup.Addon>
                </InputGroup.Root>
                <InputGroup.Root>
                    <InputGroup.Input
                        required={true}
                        placeholder="Ссылка на трек"
                        bind:value={payload.url}
                    />
                    <InputGroup.Addon align="inline-start">
                        <Link/>
                    </InputGroup.Addon>
<!--                    for some reason pasteUrl doesn't work -->
                    <InputGroup.Button
                        class="rounded-full"
                        size="icon-xs"
                        onclick={pasteUrl}
                        type="button"
                    >
                        <ClipboardPaste/>
                    </InputGroup.Button>
                </InputGroup.Root>
            <Dialog.Footer>
                <Button type="submit">Создать</Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
