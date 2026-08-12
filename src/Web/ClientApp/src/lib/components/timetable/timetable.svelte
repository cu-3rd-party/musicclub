<script lang="ts">
    import type { TimetableEvent } from "$lib/timetable/types";

    import TimetableHeader from "./timetable-header.svelte";
    import TimetableTimeColumn from "./timetable-time-column.svelte";
    import TimetableGrid from "./timetable-grid.svelte";

    let {
        date = new Date(),
        events = [],
        startHour = 0,
        endHour = 24,
        hourHeight = 80
    }: {
        date?: Date;
        events?: TimetableEvent[];
        startHour?: number;
        endHour?: number;
        hourHeight?: number;
    } = $props();

    let selectedDate = $state(date);

    function previousDay() {
        const next = new Date(selectedDate);
        next.setDate(next.getDate() - 1);
        selectedDate = next;
    }

    function nextDay() {
        const next = new Date(selectedDate);
        next.setDate(next.getDate() + 1);
        selectedDate = next;
    }

    function today() {
        selectedDate = new Date();
    }
</script>

<div class="flex h-full flex-col overflow-hidden border">
    <TimetableHeader
        date={selectedDate}
        onPrevious={previousDay}
        onNext={nextDay}
        onToday={today}
    />

    <div class="flex flex-1 overflow-y-auto">
        <TimetableTimeColumn
            {startHour}
            {endHour}
            {hourHeight}
        />

        <TimetableGrid
            {events}
            {startHour}
            {endHour}
            {hourHeight}
        />
    </div>
</div>
