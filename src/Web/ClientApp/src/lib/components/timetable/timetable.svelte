<script lang="ts">
    import type { TimetableEvent } from "$lib/timetable/types";
    import {
        addDays,
        eventsForDay,
        isSameDay,
        startOfDay,
        startOfWeek,
    } from "$lib/timetable/utils";
    import { cn } from "$lib/utils";

    import TimetableHeader from "./timetable-header.svelte";
    import TimetableTimeColumn from "./timetable-time-column.svelte";
    import TimetableGrid from "./timetable-grid.svelte";

    let {
        date = $bindable(new Date()),
        days = 1,
        events = [],
        startHour = 0,
        endHour = 24,
        hourHeight = 80,
        loading = false,
        onSelect,
        onDaySelect,
    }: {
        date?: Date;
        days?: 1 | 7;
        events?: TimetableEvent[];
        startHour?: number;
        endHour?: number;
        hourHeight?: number;
        loading?: boolean;
        onSelect?: (event: TimetableEvent) => void;
        onDaySelect?: (day: Date) => void;
    } = $props();

    const columns = $derived(
        days === 1
            ? [startOfDay(date)]
            : Array.from({ length: 7 }, (_, i) =>
                  addDays(startOfWeek(date), i),
              ),
    );

    const today = new Date();

    const weekdayFormatter = new Intl.DateTimeFormat("ru-RU", {
        weekday: "short",
    });

    let scroller = $state<HTMLDivElement | null>(null);

    // Прокручиваем к первому событию (или к текущему времени), чтобы вечерние репетиции были видны сразу
    $effect(() => {
        if (!scroller) return;

        const starts = columns.flatMap((day) =>
            eventsForDay(events, day).map((e) => e.start),
        );
        if (columns.some((day) => isSameDay(day, new Date()))) {
            const now = new Date();
            starts.push(now.getHours() * 60 + now.getMinutes());
        }
        if (starts.length === 0) return;

        const target = Math.min(...starts) - startHour * 60 - 60;
        scroller.scrollTo({
            top: Math.max(0, (target / 60) * hourHeight),
            behavior: "smooth",
        });
    });

    function shift(direction: -1 | 1) {
        date = addDays(date, direction * days);
    }

    function goToday() {
        date = new Date();
    }
</script>

<div class="flex h-full flex-col overflow-hidden border">
    <TimetableHeader
        {date}
        {days}
        onPrevious={() => shift(-1)}
        onNext={() => shift(1)}
        onToday={goToday}
    />

    <div
        bind:this={scroller}
        class={cn(
            "relative flex flex-1 overflow-auto transition-opacity",
            loading && "opacity-60",
        )}
    >
        <div class={cn("flex flex-1 flex-col", days === 7 && "min-w-[400px]")}>
            {#if days === 7}
                <!-- Дни недели -->
                <div class="sticky top-0 z-30 flex border-b bg-background">
                    <div class="w-16 shrink-0"></div>
                    {#each columns as day (day.getTime())}
                        <button
                            class={cn(
                                "flex-1 border-l py-1 text-center text-xs",
                                isSameDay(day, today) &&
                                    "font-semibold text-primary",
                            )}
                            onclick={() => {
                                date = day;
                                onDaySelect?.(day);
                            }}
                        >
                            <div class="text-muted-foreground uppercase">
                                {weekdayFormatter.format(day)}
                            </div>
                            <div class="text-base">{day.getDate()}</div>
                        </button>
                    {/each}
                </div>
            {/if}

            <!-- pt-2: подпись первого часа сдвинута вверх и иначе обрезается -->
            <div class="flex flex-1 pt-2">
                <TimetableTimeColumn {startHour} {endHour} {hourHeight} />

                {#each columns as day (day.getTime())}
                    <TimetableGrid
                        events={eventsForDay(events, day)}
                        {startHour}
                        {endHour}
                        {hourHeight}
                        showNow={isSameDay(day, today)}
                        compact={days === 7}
                        {onSelect}
                    />
                {/each}
            </div>
        </div>
    </div>
</div>
