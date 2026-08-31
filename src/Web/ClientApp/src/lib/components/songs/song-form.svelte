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
        existingRoles = [
          "Вокал",
          "Бэк-вокал",
          "Гитара",
          "Ритм-гитара",
          "Лид-гитара",
          "Акустическая гитара",
          "Электрогитара",
          "Бас-гитара",
          "Барабаны",
          "Ударные",
          "Перкуссия",
          "Пианино",
          "Фортепиано",
          "Клавишные",
          "Синтезатор",
          "Орган",
          "Флейта",
          "Скрипка",
          "Альт",
          "Виолончель",
          "Контрабас",
          "Саксофон",
          "Кларнет",
          "Труба",
          "Тромбон",
          "Валторна",
          "Гармонь",
          "Аккордеон",
          "Арфа",
          "Мандолина",
          "Банджо",
          "Укулеле",
          "Ксилофон",
          "Вибрафон",
          "Тарелки",
          "Кахон",
          "Тамбурин",
          "DJ",
          "Сэмплер",
          "Продюсер",
        ],
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
        existingRoles?: string[];
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
    let roleInputFocused = $state(false);
    let highlightedIndex = $state(-1);

    const roleSuggestions = $derived((() => {
        const query = roleInput.trim().toLowerCase();

        if (!query) return [];

        const added = new Set(
            (availableRoles ?? []).map((r) => r.toLowerCase())
        );

        return existingRoles
            .filter((r) => {
                const lower = r.toLowerCase();
                return lower.includes(query) && !added.has(lower);
            })
            .slice(0, 5);
    })());

    function selectRoleSuggestion(role: string) {
        const existing = availableRoles ?? [];

        if (!existing.some((r) => r.toLowerCase() === role.toLowerCase())) {
            availableRoles = [...existing, role];
        }

        roleInput = "";
        highlightedIndex = -1;
    }

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

            if (roleSuggestions.length > 0 && highlightedIndex >= 0) {
                selectRoleSuggestion(roleSuggestions[highlightedIndex]);
            } else {
                addRole();
            }
        } else if (event.key === "ArrowDown") {
            event.preventDefault();
            highlightedIndex = Math.min(
                highlightedIndex + 1,
                roleSuggestions.length - 1
            );
        } else if (event.key === "ArrowUp") {
            event.preventDefault();
            highlightedIndex = Math.max(highlightedIndex - 1, -1);
        } else if (event.key === "Escape") {
            highlightedIndex = -1;
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
            {#each availableRoles as role (role)}
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

    <div class="relative flex gap-2">
        <div class="relative min-w-0 flex-1">
            <InputGroup.Root>
                <InputGroup.Input
                    placeholder="гитара"
                    bind:value={roleInput}
                    onkeydown={handleRoleKeydown}
                    onfocus={() => {
                        roleInputFocused = true;
                        highlightedIndex = -1;
                    }}
                    onblur={() => {
                        roleInputFocused = false;
                    }}
                />
            </InputGroup.Root>

            {#if roleInputFocused && roleSuggestions.length > 0}
                <div
                    class="bg-popover text-popover-foreground absolute top-full z-50 mt-1 w-full overflow-hidden rounded-md border shadow-md"
                >
                    {#each roleSuggestions as role, i (role)}
                        <button
                            type="button"
                            class="flex w-full items-center px-3 py-1.5 text-sm {i === highlightedIndex
                                ? 'bg-accent text-accent-foreground'
                                : 'hover:bg-accent hover:text-accent-foreground'}"
                            onmousedown={(e) => {
                                e.preventDefault();
                                selectRoleSuggestion(role);
                            }}
                            onmouseenter={() => {
                                highlightedIndex = i;
                            }}
                        >
                            {role}
                        </button>
                    {/each}
                </div>
            {/if}
        </div>

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
