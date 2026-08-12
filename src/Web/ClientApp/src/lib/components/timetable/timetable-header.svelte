<script lang="ts">
    import {ChevronLeft, ChevronRight} from "@lucide/svelte";
    import {Button} from "$lib/components/ui/button";

    let {
        date,
        onPrevious,
        onNext,
        onToday
    }: {
        date: Date;
        onPrevious: () => void;
        onNext: () => void;
        onToday: () => void;
    } = $props();

    const formatter = new Intl.DateTimeFormat("ru-RU", {
        weekday: "short",
        day: "numeric",
        month: "long"
    });
</script>

<header class="flex items-center justify-between border-b p-3">
    <Button
        variant="ghost"
        size="icon"
        onclick={onPrevious}
        aria-label="Предыдущий день"
    >
        <ChevronLeft/>
    </Button>

    <button class="text-center" onclick={onToday}>
        <div class="font-medium">
            {formatter.format(date)}
        </div>

        <div class="text-sm text-muted-foreground">
            Сегодня
        </div>
    </button>

    <Button
        variant="ghost"
        size="icon"
        onclick={onNext}
        aria-label="Следующий день"
    >
        <ChevronRight/>
    </Button>
</header>
