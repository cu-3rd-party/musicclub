<script lang="ts">
    import type { TimetableEvent } from "$lib/timetable/types";
    import { layoutEvents } from "$lib/timetable/utils";
    import TimetableEventComponent from "./timetable-event.svelte";
    import CurrentTimeMarker from "./current-time-marker.svelte";

    let {
        events,
        startHour = 0,
        endHour = 24,
        hourHeight = 80,
        showNow = true,
        compact = false,
        onSelect,
    }: {
        events: TimetableEvent[];
        startHour?: number;
        endHour?: number;
        hourHeight?: number;
        showNow?: boolean;
        compact?: boolean;
        onSelect?: (event: TimetableEvent) => void;
    } = $props();

    const hours = $derived(
        Array.from({ length: endHour - startHour }, (_, i) => startHour + i),
    );

    const positioned = $derived(layoutEvents(events));
</script>

<div class="relative flex-1 border-l">
    <!-- Hour grid -->
    {#each hours as hour (hour)}
        <div class="relative border-b" style={`height: ${hourHeight}px`}>
            <!-- 30 minute line -->
            <div
                class="absolute inset-x-0 top-1/2 border-b border-dashed border-muted"
            ></div>
        </div>
    {/each}

    <!-- Current time -->
    {#if showNow}
        <CurrentTimeMarker {startHour} {hourHeight} />
    {/if}

    <!-- Events -->
    {#each positioned as event (event.id)}
        <TimetableEventComponent
            {event}
            {startHour}
            {hourHeight}
            {compact}
            {onSelect}
        />
    {/each}
</div>
