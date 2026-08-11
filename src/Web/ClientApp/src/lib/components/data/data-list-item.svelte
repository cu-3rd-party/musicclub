<script lang="ts">
    import {Button} from "$lib/components/ui/button/index.js";
    import {Badge} from "$lib/components/ui/badge/index.js";
    import type {KnowledgeObject} from "$lib/data/types";
    import {formatBytes, formatDateTime, getObjectTitle, getObjectTypeLabel} from "$lib/data/utils";
    import DataStatusBadge from "$lib/components/data/data-status-badge.svelte";
    import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";

    let {object}: { object: KnowledgeObject } = $props();
</script>

<article
    class="bg-card/90 rounded-[1.5rem] border border-border/60 px-5 py-5 shadow-[0_16px_40px_-32px_rgba(0,0,0,0.45)] transition-colors hover:border-border">
    <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div class="min-w-0 space-y-3">
            <div class="flex flex-wrap items-center gap-2">
                <h2 class="text-foreground line-clamp-2 text-lg font-semibold tracking-tight">{getObjectTitle(object)}</h2>
                <DataStatusBadge status={object.status}/>
            </div>

            <div class="text-muted-foreground flex flex-wrap gap-x-4 gap-y-2 text-sm">
                <span>{getObjectTypeLabel(object)}</span>
                <span>{formatBytes(object.size)}</span>
                <span>Загружен {formatDateTime(object.createdAt)}</span>
                {#if object.createdBy?.name || object.createdBy?.email}
                    <span>Автор: {object.createdBy.name || object.createdBy.email}</span>
                {/if}
            </div>

            {#if object.tags?.length}
                <div class="flex flex-wrap gap-2">
                    {#each object.tags as tag (tag)}
                        <Badge variant="outline">{tag}</Badge>
                    {/each}
                </div>
            {/if}

            {#if object.errorMessage}
                <p class="text-destructive text-sm leading-6">{object.errorMessage}</p>
            {/if}
        </div>

        <div class="flex shrink-0 items-center gap-2">
            <Button href={`/data/${object.id}`} variant="outline" class="rounded-full">
                Открыть
                <ChevronRightIcon class="size-4"/>
            </Button>
        </div>
    </div>
</article>
