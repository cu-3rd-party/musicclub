<script lang="ts">
    import type {TimetableEvent} from "$lib/timetable/types";
    import TimetableEventComponent from "./timetable-event.svelte";
    import CurrentTimeMarker from "./current-time-marker.svelte";

    let {
        events,
        startHour = 0,
        endHour = 24,
        hourHeight = 80
    }: {
        events: TimetableEvent[];
        startHour?: number;
        endHour?: number;
        hourHeight?: number;
    } = $props();

    const hours = $derived(
        Array.from(
            {length: endHour - startHour},
            (_, i) => startHour + i
        )
    );
</script>

<div class="relative flex-1 border-l">
    <!-- Hour grid -->
    {#each hours as hour}
        <div
            class="relative border-b"
            style={`height: ${hourHeight}px`}
        >
            <!-- 30 minute line -->
            <div
                class="absolute inset-x-0 top-1/2 border-b border-dashed border-muted"
            ></div>
        </div>
    {/each}

    <!-- Current time -->
    <CurrentTimeMarker
        {startHour}
        {hourHeight}
    />

    <!-- Events -->
    {#each events as event (event.id)}
        <TimetableEventComponent
            {event}
            {startHour}
            {hourHeight}
        />
    {/each}
</div>
