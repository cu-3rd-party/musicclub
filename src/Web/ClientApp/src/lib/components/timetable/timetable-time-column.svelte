<script lang="ts">
    let {
        startHour,
        endHour,
        hourHeight
    }: {
        startHour: number;
        endHour: number;
        hourHeight: number;
    } = $props();

    const hours = $derived(
        Array.from(
            { length: endHour - startHour + 1 },
            (_, i) => startHour + i
        )
    );
</script>

<div class="w-16 shrink-0">
    {#each hours as hour}
        <div
            class="relative text-right text-xs text-muted-foreground"
            style={`height: ${hourHeight}px`}
        >
            <span class="relative -top-2 pr-2">
                {String(hour).padStart(2, "0")}:00
            </span>
        </div>
    {/each}
</div>
