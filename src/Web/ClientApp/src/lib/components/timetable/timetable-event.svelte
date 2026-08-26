<script lang="ts">
    import type {TimetableEvent} from "$lib/timetable/types";
    import {resolve} from "$app/paths";
    import {goto} from "$app/navigation";

    let {
        event,
        hourHeight,
        startHour
    }: {
        event: TimetableEvent;
        hourHeight: number;
        startHour: number;
    } = $props();

    const top = $derived(
        ((event.start - startHour * 60) / 60) * hourHeight
    );

    const height = $derived(
        ((event.end - event.start) / 60) * hourHeight
    );
</script>

<button
    class="absolute left-1 right-2 overflow-hidden rounded-md bg-primary p-2 text-sm text-primary-foreground shadow-sm"
    style={`top: ${top}px; height: ${height}px`}
    onclick={() => goto(resolve(`/app/songs/${event.song.id}`))}
>
    <span class="font-medium">
        {event.song.title}
    </span>
</button>
