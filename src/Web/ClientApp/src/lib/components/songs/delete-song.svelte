<script lang="ts">
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import {Trash2} from "@lucide/svelte";
    import {Button, buttonVariants} from "$lib/components/ui/button";
    import * as AlertDialog from "$lib/components/ui/alert-dialog";
    import {deleteSong} from "$lib/api/songs";
    import type {Song} from "$lib/songs/types";
    import {Permission} from "$lib/permissions/resolve";
    import {cn} from "$lib/utils";

    let {
        song,
        currentUser,
    }: {
        song: Song;
        currentUser: {permissions: string[]} | null;
    } = $props();

    let dialogOpen = $state(false);
    let deleting = $state(false);
    let error = $state<string | null>(null);

    const canDelete = $derived(
        (currentUser?.permissions ?? []).includes(Permission.SongsDelete)
    );

    async function remove() {
        if (deleting) return;
        deleting = true;
        error = null;
        try {
            await deleteSong(song.id);
            dialogOpen = false;
            await goto(resolve("/app/songs"));
        } catch (err) {
            console.error(err);
            error = "Не удалось удалить песню";
        } finally {
            deleting = false;
        }
    }
</script>

{#if canDelete}
    <AlertDialog.Root bind:open={dialogOpen}>
        <AlertDialog.Trigger
            type="button"
            class={cn(buttonVariants({variant: "ghost", size: "icon"}), "text-destructive")}
            aria-label="Удалить песню"
        >
            <Trash2 class="size-5"/>
        </AlertDialog.Trigger>

        <AlertDialog.Content>
            <AlertDialog.Header>
                <AlertDialog.Title>Удалить песню?</AlertDialog.Title>
                <AlertDialog.Description>
                    «{song.title}» и её топик в Telegram будут удалены без возможности восстановления.
                </AlertDialog.Description>
            </AlertDialog.Header>

            {#if error}
                <p class="text-sm text-destructive">{error}</p>
            {/if}

            <AlertDialog.Footer>
                <AlertDialog.Cancel disabled={deleting}>Отмена</AlertDialog.Cancel>
                <AlertDialog.Action
                    variant="destructive"
                    disabled={deleting}
                    onclick={(e) => {
                        e.preventDefault();
                        remove();
                    }}
                >
                    {deleting ? "Удаление..." : "Удалить"}
                </AlertDialog.Action>
            </AlertDialog.Footer>
        </AlertDialog.Content>
    </AlertDialog.Root>
{/if}
