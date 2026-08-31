<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group";
    import {
        ClipboardPaste,
        Link,
        MessageSquare,
        Music2,
        Upload,
        User,
        X
    } from "@lucide/svelte";
    import {Button} from "$lib/components/ui/button";
    import {Badge} from "$lib/components/ui/badge";
    import {fetchYouTubeThumbnail} from "$lib/songs/helpers";
    import {createData} from "$lib/api/data";
    import type {UUID} from "node:crypto";

    let {
        title = $bindable(""),
        artist = $bindable(""),
        description = $bindable<string | null>(null),
        url = $bindable(""),
        featured = $bindable(false),
        availableRoles = $bindable<string[] | null>(null),
        thumbnailUrl = $bindable<string | null>(null),
        submitLabel = "Создать",
        submittingLabel = "Создание...",
        onsubmit,
    }: {
        title?: string;
        artist?: string;
        description?: string | null;
        url?: string;
        featured?: boolean;
        availableRoles?: string[] | null;
        thumbnailUrl?: string | null;
        submitLabel?: string;
        submittingLabel?: string;
        onsubmit: (payload: {
            title: string;
            artist: string;
            description: string | null;
            url: string;
            thumbnailDataEntryId: UUID | null;
            featured: boolean;
            availableRoles: string[] | null;
        }) => Promise<void>;
    } = $props();

    let roleInput = $state("");
    let thumbnailFile = $state<File | null>(null);
    let thumbnailPreviewUrl = $state<string | null>(null);
    let submitting = $state(false);

    $effect(() => {
        return () => {
            if (thumbnailPreviewUrl) {
                URL.revokeObjectURL(thumbnailPreviewUrl);
            }
        };
    });

    function applyThumbnail(file: File | null) {
        if (thumbnailPreviewUrl) {
            URL.revokeObjectURL(thumbnailPreviewUrl);
        }

        thumbnailFile = file;
        thumbnailPreviewUrl = file ? URL.createObjectURL(file) : null;
    }

    async function updateThumbnailFromUrl() {
        const file = await fetchYouTubeThumbnail(url);

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

        return await createData(thumbnailFile);
    }

    function addRole() {
        const input = roleInput.trim();

        if (!input) return;

        const newRoles = input
            .split(",")
            .map((role) => role.trim())
            .filter(Boolean);

        const existingRoles = availableRoles ?? [];
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

        availableRoles = roles.length > 0 ? roles : null;
        roleInput = "";
    }

    function removeRole(role: string) {
        const roles =
            availableRoles?.filter((existingRole) => existingRole !== role) ?? [];

        availableRoles = roles.length > 0 ? roles : null;
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
            const roles = availableRoles
                ?.map((role) => role.trim())
                .filter(Boolean) ?? [];

            const thumbnailDataEntryId = await createThumbnailData();

            await onsubmit({
                title: title.trim(),
                artist: artist.trim(),
                url: url.trim(),
                description: description?.trim() || null,
                availableRoles: roles.length > 0 ? roles : null,
                thumbnailDataEntryId,
                featured,
            });

            clearThumbnail();
        } finally {
            submitting = false;
        }
    }

    async function pasteUrl() {
        url = await navigator.clipboard.readText();
        await updateThumbnailFromUrl();
    }
</script>

<form onsubmit={handleSubmit} class="grid gap-4">
    <InputGroup.Root>
        <InputGroup.Input
            required
            placeholder="Название"
            bind:value={title}
        />
        <InputGroup.Addon>
            <Music2/>
        </InputGroup.Addon>
    </InputGroup.Root>

    <InputGroup.Root>
        <InputGroup.Input
            required
            placeholder="Исполнитель"
            bind:value={artist}
        />
        <InputGroup.Addon>
            <User/>
        </InputGroup.Addon>
    </InputGroup.Root>

    <InputGroup.Root>
        <InputGroup.Textarea
            placeholder="Добавь комментарий"
            bind:value={description}
        />
        <InputGroup.Addon align="block-end">
            <MessageSquare/>
        </InputGroup.Addon>
    </InputGroup.Root>

    {#if availableRoles?.length}
        <div class="flex flex-wrap gap-2">
            {#each availableRoles as role}
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

    <InputGroup.Root>
        <InputGroup.Input
            required
            placeholder="Ссылка на трек"
            bind:value={url}
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
        {:else if thumbnailUrl}
            <img
                src={thumbnailUrl}
                alt="Предпросмотр обложки"
                class="absolute inset-0 size-full object-cover"
            />
        {/if}

        <span
            class="{(thumbnailPreviewUrl || thumbnailUrl)
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

        {#if thumbnailPreviewUrl || thumbnailUrl}
            <button
                type="button"
                class="bg-background/60 backdrop-blur hover:bg-background absolute top-2 right-2 z-10 flex size-7 items-center justify-center rounded-full"
                onclick={(e) => {
                    e.preventDefault();
                    clearThumbnail();
                    thumbnailUrl = null;
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

    <Button
        type="submit"
        disabled={submitting}
    >
        {#if submitting}
            {submittingLabel}
        {:else}
            {submitLabel}
        {/if}
    </Button>
</form>
