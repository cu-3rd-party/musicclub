<script lang="ts">
    import type { CalendarEvent } from "$lib/api/calendar";
    import { CalendarDaysIcon, MapPinIcon, UserIcon, MusicIcon } from "@lucide/svelte";
    import { formatDateRU } from "$lib/utils";

    let { event }: { event: CalendarEvent } = $props();

    const eventTypeConfig = {
        Rehearsal: { icon: MusicIcon, label: "Репетиция", color: "text-blue-500" },
        Performance: { icon: UserIcon, label: "Выступление", color: "text-purple-500" },
        Personal: { icon: CalendarDaysIcon, label: "Личное", color: "text-green-500" },
    } as const;

    const config = eventTypeConfig[event.eventType as keyof typeof eventTypeConfig] || eventTypeConfig.Personal;
    const Icon = config.icon;

    const formattedDate = formatDateRU(event.startAt);
</script>

<div
    class="flex items-start gap-3 rounded-lg border bg-card p-3 shadow-sm transition-colors hover:bg-muted/50"
>
    <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-muted">
        <Icon class="h-5 w-5 {config.color}" />
    </div>

    <div class="flex-1 space-y-1">
        <div class="flex items-center justify-between">
            <p class="text-sm font-medium leading-none">{event.title}</p>
            <span class="text-xs text-muted-foreground">{config.label}</span>
        </div>

        <p class="text-xs text-muted-foreground">
            {formattedDate}
        </p>

        {#if event.location}
            <div class="flex items-center gap-1 text-xs text-muted-foreground">
                <MapPinIcon class="h-3 w-3" />
                <span>{event.location}</span>
            </div>
        {/if}

        {#if event.description}
            <p class="text-xs text-muted-foreground line-clamp-2">
                {event.description}
            </p>
        {/if}
    </div>
</div>
