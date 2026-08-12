<script lang="ts">
    import type {TimetableEvent} from "$lib/timetable/types";

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

<div
    class="absolute left-1 right-2 overflow-hidden rounded-md bg-primary p-2 text-sm text-primary-foreground shadow-sm"
    style={`top: ${top}px; height: ${height}px`}
>
    <div class="font-medium">
        {event.title}
    </div>
</div>
