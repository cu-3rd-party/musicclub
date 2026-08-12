<script lang="ts">
    import type {Snippet} from "svelte";
    import {Button} from "$lib/components/ui/button";
    import ArrowLeftIcon from "@lucide/svelte/icons/arrow-left";

    let {
        title,
        description = "",
        backHref,
        backLabel = "Назад",
        actions,
    }: {
        title: string;
        description?: string;
        backHref?: string;
        backLabel?: string;
        actions?: Snippet;
    } = $props();
</script>

<div class="flex flex-col gap-4">
    {#if backHref}
        <div>
            <Button href={backHref} variant="ghost" class="-ml-2 w-fit rounded-full">
                <ArrowLeftIcon class="size-4"/>
                {backLabel}
            </Button>
        </div>
    {/if}

    <div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div class="space-y-3">
            <div class="space-y-1">
                <h1 class="text-foreground text-3xl font-semibold tracking-tight md:text-4xl">{title}</h1>
                {#if description}
                    <p class="text-muted-foreground max-w-3xl text-sm leading-6 md:text-base">{description}</p>
                {/if}
            </div>
        </div>

        <div class="flex flex-wrap items-center gap-2">
            {@render actions?.()}
        </div>
    </div>
</div>
