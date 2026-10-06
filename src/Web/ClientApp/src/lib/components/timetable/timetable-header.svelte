<script lang="ts">
    import { ChevronLeft, ChevronRight } from "@lucide/svelte";
    import { Button } from "$lib/components/ui/button";
    import { addDays, isSameDay, startOfWeek } from "$lib/timetable/utils";

    let {
        date,
        days = 1,
        onPrevious,
        onNext,
        onToday,
    }: {
        date: Date;
        days?: 1 | 7;
        onPrevious: () => void;
        onNext: () => void;
        onToday: () => void;
    } = $props();

    const dayFormatter = new Intl.DateTimeFormat("ru-RU", {
        weekday: "short",
        day: "numeric",
        month: "long",
    });

    const shortFormatter = new Intl.DateTimeFormat("ru-RU", {
        day: "numeric",
        month: "short",
    });

    const title = $derived.by(() => {
        if (days === 1) return dayFormatter.format(date);

        const weekStart = startOfWeek(date);
        const weekEnd = addDays(weekStart, 6);
        return `${shortFormatter.format(weekStart)} – ${shortFormatter.format(weekEnd)}`;
    });

    const isCurrent = $derived(
        days === 1
            ? isSameDay(date, new Date())
            : isSameDay(startOfWeek(date), startOfWeek(new Date())),
    );
</script>

<header class="flex items-center justify-between border-b p-3">
    <Button
        variant="ghost"
        size="icon"
        onclick={onPrevious}
        aria-label={days === 1 ? "Предыдущий день" : "Предыдущая неделя"}
    >
        <ChevronLeft />
    </Button>

    <button class="text-center" onclick={onToday}>
        <div class="font-medium">
            {title}
        </div>

        <div class="text-sm text-muted-foreground">
            {#if isCurrent}
                {days === 1 ? "Сегодня" : "Эта неделя"}
            {:else}
                <span class="text-primary">Вернуться к сегодня</span>
            {/if}
        </div>
    </button>

    <Button
        variant="ghost"
        size="icon"
        onclick={onNext}
        aria-label={days === 1 ? "Следующий день" : "Следующая неделя"}
    >
        <ChevronRight />
    </Button>
</header>
