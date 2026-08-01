<script lang="ts">
    import {Button} from "$lib/components/ui/button/index.js";
    import FileSearchIcon from "@lucide/svelte/icons/file-search";

    let {
        title,
        description,
        actionLabel,
        actionHref,
    }: {
        title: string;
        description: string;
        actionLabel?: string;
        actionHref?: string;
    } = $props();
</script>

<div class="bg-card/90 flex flex-col items-center justify-center rounded-[1.75rem] border border-border/60 px-6 py-16 text-center shadow-[0_18px_60px_-36px_rgba(0,0,0,0.35)]">
    <div class="bg-muted text-muted-foreground mb-5 flex size-14 items-center justify-center rounded-2xl">
        <FileSearchIcon class="size-6"/>
    </div>
    <h2 class="text-foreground text-xl font-semibold">{title}</h2>
    <p class="text-muted-foreground mt-2 max-w-md text-sm leading-6">{description}</p>
    {#if actionLabel && actionHref}
        <div class="mt-6">
            <Button href={actionHref}>{actionLabel}</Button>
        </div>
    {/if}
</div>
