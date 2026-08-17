<script lang="ts">
    import * as InputGroup from "$lib/components/ui/input-group";
    import {
        ClipboardPaste,
        Link,
        MessageSquare,
        Music2,
        Plus,
        User,
        X
    } from "@lucide/svelte";
    import {Button, buttonVariants} from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog";
    import type {WithElementRef} from "$lib/utils";
    import type {HTMLFormAttributes} from "svelte/elements";
    import type {CreateSongPayload} from "$lib/songs/types";
    import {createSong} from "$lib/api/songs";
    import {Badge} from "$lib/components/ui/badge";

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
        payload?: CreateSongPayload;
    } = $props();

    let dialogOpen = $state(false);
    let roleInput = $state("");

    function addRole() {
        const input = roleInput.trim();

        if (!input) return;

        // Allow both individual roles and comma-separated roles.
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

        const roles = payload.availableRoles
            ?.map((role) => role.trim())
            .filter(Boolean) ?? [];

        await createSong({
            ...payload,
            title: payload.title.trim(),
            artist: payload.artist.trim(),
            url: payload.url.trim(),
            description: payload.description?.trim() || null,
            availableRoles: roles.length > 0 ? roles : null,
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

            <!-- Required roles -->
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
                                <X class="size-3" />
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

            <Dialog.Footer>
                <Button type="submit">
                    Создать
                </Button>
            </Dialog.Footer>
        </form>
    </Dialog.Content>
</Dialog.Root>
