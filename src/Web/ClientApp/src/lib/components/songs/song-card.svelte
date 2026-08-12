<script lang="ts">
    import * as Card from "$lib/components/ui/card";
    import {Star} from "@lucide/svelte";
    import type {WithElementRef} from "bits-ui";
    import type {HTMLFormAttributes} from "svelte/elements";
    import {Label} from "$lib/components/ui/label";
    import {Badge} from "$lib/components/ui/badge";

    let {
        ref = $bindable(null),
        class: className,
        title,
        artist,
        description,
        featured = false,
        imageUrl = "https://placehold.co/1000x1000",
        filledAssignments = 0,
        totalAssignments = 0,
        ...restProps
    }: WithElementRef<HTMLFormAttributes> & {
        title: string,
        artist: string,
        description?: string,
        featured?: boolean,
        imageUrl?: string,
        filledAssignments?: number,
        totalAssignments?: number,
    } = $props();
</script>

<Card.Root class="relative w-full pt-0 {className}">
    <div class="relative aspect-video">
        <img
            src={imageUrl}
            alt="placeholder"
            class="h-full w-full object-cover"
        />

        <div class="absolute inset-0">
            {#if featured}
                <Card.Action class="absolute top-2 right-2">
                    <Star class="size-6"/>
                </Card.Action>
            {/if}

            {#if totalAssignments !== 0}
                <Badge class="absolute right-2 bottom-2 bg-black/60 text-white">
                    {filledAssignments}/{totalAssignments}
                </Badge>
            {/if}
        </div>
    </div>
    <Card.Header>
        <div class="flex justify-between">
            <Card.Title>{title}</Card.Title>
            <Card.Title class="text-right">{artist}</Card.Title>
        </div>
        {#if description !== undefined}
            <Card.Description>{description}</Card.Description>
        {/if}
    </Card.Header>
</Card.Root>
