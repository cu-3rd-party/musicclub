<script lang="ts">
    let {
        hourHeight,
        startHour
    }: {
        hourHeight: number;
        startHour: number;
    } = $props();

    let now = $state(new Date());

    const top = $derived.by(() => {
        const minutes =
            now.getHours() * 60 +
            now.getMinutes() +
            now.getSeconds() / 60;

        return ((minutes - startHour * 60) / 60) * hourHeight;
    });

    const visible = $derived(
        now.getHours() >= startHour
    );

    $effect(() => {
        const interval = setInterval(() => {
            now = new Date();
        }, 30_000);

        return () => clearInterval(interval);
    });
</script>

{#if visible}
    <div
        class="pointer-events-none absolute inset-x-0 z-20 border-t-2 border-red-500"
        style={`top: ${top}px`}
    >
        <div
            class="absolute -left-1 -top-[5px] size-2 rounded-full bg-red-500"
        ></div>
    </div>
{/if}
