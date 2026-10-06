<script lang="ts">
    import type {
        PositionedTimetableEvent,
        TimetableEvent,
    } from "$lib/timetable/types";
    import { cn } from "$lib/utils";

    let {
        event,
        hourHeight,
        startHour,
        compact = false,
        onSelect,
    }: {
        event: PositionedTimetableEvent;
        hourHeight: number;
        startHour: number;
        compact?: boolean;
        onSelect?: (event: TimetableEvent) => void;
    } = $props();

    const top = $derived(((event.start - startHour * 60) / 60) * hourHeight);

    // минимальная высота, чтобы короткие события можно было нажать
    const height = $derived(
        Math.max(((event.end - event.start) / 60) * hourHeight, 18),
    );

    const width = $derived(100 / event.lanes);

    const kindClass: Record<TimetableEvent["kind"], string> = {
        Rehearsal: "bg-primary text-primary-foreground",
        Performance: "bg-amber-500 text-white",
        Personal: "bg-sky-600 text-white",
        External:
            "border border-dashed border-muted-foreground/40 bg-muted text-muted-foreground",
    };

    const timeFormatter = new Intl.DateTimeFormat("ru-RU", {
        hour: "2-digit",
        minute: "2-digit",
    });

    const pending = $derived(
        event.kind === "Rehearsal" && event.status === "Pending",
    );
</script>

<button
    class={cn(
        "absolute overflow-hidden rounded-md text-left shadow-sm",
        compact ? "p-1 text-[11px] leading-tight" : "p-2 text-sm",
        kindClass[event.kind],
        pending && "opacity-60",
    )}
    style={`top: ${top}px; height: ${height}px; left: calc(${event.lane * width}% + 2px); width: calc(${width}% - 4px)`}
    title={event.title}
    onclick={() => onSelect?.(event)}
>
    <span class="line-clamp-2 font-medium break-words">
        {event.title}
    </span>
    {#if !compact && height >= 40}
        <span class="block text-xs opacity-80">
            {timeFormatter.format(event.startAt)}–{timeFormatter.format(
                event.endAt,
            )}
        </span>
    {/if}
</button>
