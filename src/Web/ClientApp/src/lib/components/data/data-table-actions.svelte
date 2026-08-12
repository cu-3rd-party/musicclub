<script lang="ts">
    import Trash2Icon from "@lucide/svelte/icons/trash-2";
    import {buttonVariants} from "$lib/components/ui/button";
    import {cn} from "$lib/utils.js";
    import {resolve} from "$app/paths";

    type Props = {
        id: string;
        isDeleteConfirming?: boolean;
        isDeleting?: boolean;
        onDeleteClick: (id: string) => void;
    };

    let {
        id,
        isDeleteConfirming = false,
        isDeleting = false,
        onDeleteClick,
    }: Props = $props();

    const openButtonClass = cn(buttonVariants({variant: "outline", size: "sm"}), "rounded-full");
    const deleteButtonClass = $derived(
        cn(
            buttonVariants({variant: "outline", size: "icon"}),
            "rounded-full transition-colors",
            isDeleteConfirming && "border-destructive bg-destructive text-destructive-foreground hover:bg-destructive/90 hover:text-destructive-foreground",
        ),
    );
</script>

<div class="flex items-center justify-end gap-2">
    <a href={resolve(`/data/${id}`)} class={openButtonClass}>Открыть</a>
    <button
        type="button"
        class={deleteButtonClass}
        aria-label="Удалить материал"
        title={isDeleteConfirming ? "Нажмите еще раз, чтобы удалить" : "Удалить материал"}
        disabled={isDeleting}
        onclick={() => onDeleteClick(id)}
    >
        <Trash2Icon class="size-4"/>
    </button>
</div>
