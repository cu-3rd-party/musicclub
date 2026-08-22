<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group";
    import {
        ClipboardPaste,
        Link,
        MessageSquare,
        Music2,
        Plus,
        Upload,
        User,
        X
    } from "@lucide/svelte";
    import {Button, buttonVariants} from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog";
    import type {WithElementRef} from "$lib/utils";
    import type {HTMLFormAttributes} from "svelte/elements";
    import type {CreateSongPayload} from "$lib/songs/types";
    import {createSong} from "$lib/api/songs";
    import {createData} from "$lib/api/data";
    import {Badge} from "$lib/components/ui/badge";
    import {fetchYouTubeThumbnail} from "$lib/songs/helpers";
    import type {UUID} from "node:crypto";

    let {
        ref = $bindable(null),
        class: className,
        payload = $bindable<CreateSongPayload>({
            title: "",
            artist: "",
            description: null,
            url: "",
            thumbnailId: null,
            featured: false,
            availableRoles: null,
        }),
        ...restProps
    }: WithElementRef<HTMLFormAttributes> & {
        payload?: CreateSongPayload;
    } = $props();

    let dialogOpen = $state(false);
    let roleInput = $state("");

    let thumbnailFile = $state<File | null>(null);
    let thumbnailPreviewUrl = $state<string | null>(null);
    let submitting = $state(false);

    function applyThumbnail(file: File | null) {
        if (thumbnailPreviewUrl) {
            URL.revokeObjectURL(thumbnailPreviewUrl);
        }

        thumbnailFile = file;
        thumbnailPreviewUrl = file ? URL.createObjectURL(file) : null;
    }

    async function updateThumbnailFromUrl() {
        const file = await fetchYouTubeThumbnail(payload.url);

        if (file) {
            applyThumbnail(file);
        }
    }

    function handleThumbnailFile(event: Event) {
        const input = event.currentTarget as HTMLInputElement;
        const file = input.files?.[0];

        if (!file) {
            return;
        }

        if (!file.type.startsWith("image/")) {
            input.value = "";
            return;
        }

        applyThumbnail(file);
    }

    function clearThumbnail() {
        applyThumbnail(null);
    }

    async function createThumbnailData(): Promise<UUID | null> {
        if (!thumbnailFile) {
            return null;
        }

        const dataId = await createData(thumbnailFile);

        return `/api/v1/data/${dataId}`;
    }

    function addRole() {
        const input = roleInput.trim();

        if (!input) return;

        const newRoles = input
            .split(",")
            .map((role) => role.trim())
            .filter(Boolean);

        const existingRoles = payload.availableRoles ?? [];
        const roles = [...existingRoles];

        for (const role of newRoles) {
            const alreadyExists = roles.some(
                (existingRole) =>
                    existingRole.toLowerCase() === role.toLowerCase()
            );

            if (!alreadyExists) {
                roles.push(role);
            }
        }

        payload.availableRoles = roles.length > 0 ? roles : null;
        roleInput = "";
    }

    function removeRole(role: string) {
        const roles =
            payload.availableRoles?.filter((existingRole) => existingRole !== role) ?? [];

        payload.availableRoles = roles.length > 0 ? roles : null;
    }

    function handleRoleKeydown(event: KeyboardEvent) {
        if (event.key === "Enter" || event.key === ",") {
            event.preventDefault();
            addRole();
        }
    }

    async function handleSubmit(event: SubmitEvent) {
        event.preventDefault();

        if (submitting) {
            return;
        }

        submitting = true;

        try {
            const roles = payload.availableRoles
                ?.map((role) => role.trim())
                .filter(Boolean) ?? [];

            // The DataEntry is created ONLY here, when the form is submitted.
            const thumbnailId = await createThumbnailData();

            await createSong({
                ...payload,
                title: payload.title.trim(),
                artist: payload.artist.trim(),
                url: payload.url.trim(),
                description: payload.description?.trim() || null,
                availableRoles: roles.length > 0 ? roles : null,
                thumbnailId,
            });

            clearThumbnail();
            dialogOpen = false;
        } finally {
            submitting = false;
        }
    }

    async function pasteUrl() {
        payload.url = await navigator.clipboard.readText();
        await updateThumbnailFromUrl();
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
        <form onsubmit={handleSubmit} class="grid gap-4">
            <Dialog.Header>
                <Dialog.Title>Создать песню</Dialog.Title>
            </Dialog.Header>

            <InputGroup.Root>
                <InputGroup.Input
                    required
                    placeholder="Название"
                    bind:value={payload.title}
                />
                <InputGroup.Addon>
                    <Music2/>
                </InputGroup.Addon>
            </InputGroup.Root>

            <InputGroup.Root>
                <InputGroup.Input
                    required
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

            {#if payload.availableRoles?.length}
                <div class="flex flex-wrap gap-2">
                    {#each payload.availableRoles as role}
                        <Badge variant="secondary">
                            <span class="break-all">{role}</span>

                            <button
                                type="button"
                                class="text-muted-foreground hover:text-foreground -mr-1 flex size-4 shrink-0 items-center justify-center rounded-full"
                                onclick={() => removeRole(role)}
                                aria-label={`Удалить роль ${role}`}
                            >
                                <X class="size-3"/>
                            </button>
                        </Badge>
                    {/each}
                </div>
            {/if}

            <div class="flex gap-2">
                <InputGroup.Root class="min-w-0 flex-1">
                    <InputGroup.Input
                        placeholder="гитара"
                        bind:value={roleInput}
                        onkeydown={handleRoleKeydown}
                    />
                </InputGroup.Root>

                <Button
                    type="button"
                    variant="outline"
                    onclick={addRole}
                    class="shrink-0"
                >
                    Добавить
                </Button>
            </div>

            <!-- Song URL -->
            <InputGroup.Root>
                <InputGroup.Input
                    required
                    placeholder="Ссылка на трек"
                    bind:value={payload.url}
                    oninput={updateThumbnailFromUrl}
                />

                <InputGroup.Addon align="inline-start">
                    <Link/>
                </InputGroup.Addon>

                <InputGroup.Button
                    class="rounded-full"
                    size="icon-xs"
                    onclick={pasteUrl}
                    type="button"
                    aria-label="Вставить ссылку"
                >
                    <ClipboardPaste/>
                </InputGroup.Button>
            </InputGroup.Root>

            <label
                class="border-input hover:bg-accent/50 relative flex cursor-pointer items-center justify-center overflow-hidden rounded-lg border border-dashed transition aspect-video"
            >
                {#if thumbnailPreviewUrl}
                    <img
                        src={thumbnailPreviewUrl}
                        alt="Предпросмотр обложки"
                        class="absolute inset-0 size-full object-cover"
                    />
                {/if}

                <span
                    class="{thumbnailPreviewUrl
                        ? 'bg-background/60 backdrop-blur'
                        : ''} flex flex-col items-center gap-2 text-center"
                >
                    <Upload class="size-5"/>
                    <span class="text-sm">
                        Выбрать изображение
                    </span>
                    <span class="text-muted-foreground text-xs">
                        JPG, PNG, WebP
                    </span>
                </span>

                {#if thumbnailPreviewUrl}
                    <button
                        type="button"
                        class="bg-background/60 backdrop-blur hover:bg-background absolute top-2 right-2 z-10 flex size-7 items-center justify-center rounded-full"
                        onclick={(e) => {
                            e.preventDefault();
                            clearThumbnail();
                        }}
                        aria-label="Удалить обложку"
                    >
                        <X class="size-4"/>
                    </button>
                {/if}

                <input
                    type="file"
                    class="absolute inset-0 size-full cursor-pointer opacity-0"
                    accept="image/jpeg,image/png,image/webp"
                    onchange={handleThumbnailFile}
                />
            </label>

            <Dialog.Footer>
                <Button
                    type="submit"
                    disabled={submitting}
                >
                    {#if submitting}
                        Создание...
                    {:else}
                        Создать
                    {/if}
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
