<script lang="ts">
    import type { CalendarEvent } from "$lib/api/calendar";
    import CalendarEventItem from "./CalendarEventItem.svelte";
    import { Button } from "$lib/components/ui/button";

    let {
        events,
        onViewAll,
    }: {
        events: CalendarEvent[];
        onViewAll?: () => void;
    } = $props();
</script>

<div class="space-y-3">
    {#if events.length === 0}
        <div class="flex flex-col items-center justify-center rounded-lg border border-dashed bg-muted/30 p-8 text-center">
            <p class="text-sm text-muted-foreground">Нет предстоящих событий</p>
        </div>
    {:else}
        {#each events as event (event.id)}
            <CalendarEventItem {event} />
        {/each}

        {#if onViewAll && events.length >= 5}
            <div class="flex justify-center pt-2">
                <Button variant="outline" size="sm" onclick={onViewAll}>
                    Показать все
                </Button>
            </div>
        {/if}
    {/if}
</div>
