<script lang="ts">
    import Timetable from "$lib/components/timetable/timetable.svelte";
    import type { TimetableEvent } from "$lib/timetable/types";
    import { addDays, startOfDay, startOfWeek } from "$lib/timetable/utils";
    import {
        createCalendarEvent,
        deleteCalendarEvent,
        getTimetable,
        type TimetableEventDto,
        type TimetableScope,
    } from "$lib/api/calendar";
    import { Button } from "$lib/components/ui/button";
    import * as Dialog from "$lib/components/ui/dialog";
    import * as Tabs from "$lib/components/ui/tabs";
    import { Input } from "$lib/components/ui/input";
    import { Label } from "$lib/components/ui/label";
    import { Textarea } from "$lib/components/ui/textarea";
    import { Plus } from "@lucide/svelte";
    import { goto } from "$app/navigation";
    import { resolve } from "$app/paths";

    type View = "day" | "week";

    const DEFAULT_START_HOUR = 8;
    const DEFAULT_END_HOUR = 23;

    let scope = $state<TimetableScope>("mine");
    let view = $state<View>("day");
    let date = $state(new Date());

    let events = $state<TimetableEvent[]>([]);
    let loading = $state(false);
    let error = $state<string | null>(null);

    const range = $derived.by(() => {
        const from = view === "day" ? startOfDay(date) : startOfWeek(date);
        return { from, to: addDays(from, view === "day" ? 1 : 7) };
    });

    // Расширяем сетку, если есть события раньше 8:00 или позже 23:00
    const startHour = $derived(
        Math.min(
            DEFAULT_START_HOUR,
            ...events
                .filter((e) => e.startAt >= range.from)
                .map((e) => e.startAt.getHours()),
        ),
    );
    const endHour = $derived(
        Math.max(
            DEFAULT_END_HOUR,
            ...events
                .filter((e) => e.endAt < range.to)
                .map((e) =>
                    Math.min(
                        24,
                        e.endAt.getHours() + (e.endAt.getMinutes() > 0 ? 1 : 0),
                    ),
                ),
        ),
    );

    function toTimetableEvent(dto: TimetableEventDto): TimetableEvent {
        return {
            id: dto.id,
            title: dto.title,
            kind: dto.kind,
            startAt: new Date(dto.startAt),
            endAt: new Date(dto.endAt),
            start: 0,
            end: 0,
            songId: dto.songId ?? null,
            status: dto.status ?? null,
            location: dto.location ?? null,
            canDelete: dto.canDelete,
        };
    }

    let requestId = 0;

    async function load() {
        const current = ++requestId;
        const { from, to } = range;
        loading = true;
        error = null;

        try {
            const dtos = await getTimetable(
                scope,
                from.toISOString(),
                to.toISOString(),
            );
            if (current !== requestId) return;
            events = dtos.map(toTimetableEvent);
        } catch (err) {
            if (current !== requestId) return;
            console.error("Не удалось загрузить расписание", err);
            error = "Не удалось загрузить расписание";
            events = [];
        } finally {
            if (current === requestId) loading = false;
        }
    }

    $effect(() => {
        // перезагружаем при смене вкладки или интервала
        void scope;
        void range;
        load();
    });

    function onScopeChange(value: string) {
        scope = value as TimetableScope;
        // Музклуб удобнее смотреть неделей
        if (scope === "club") view = "week";
    }

    // ---------- Детали события ----------

    let selected = $state<TimetableEvent | null>(null);

    const kindLabel: Record<TimetableEvent["kind"], string> = {
        Rehearsal: "Репетиция",
        Performance: "Выступление",
        Personal: "Личное событие",
        External: "Яндекс.Календарь",
    };

    const statusLabel: Record<string, string> = {
        Pending: "ожидает подтверждения",
        Approved: "подтверждена",
        Confirmed: "подтверждена",
    };

    const timeFormatter = new Intl.DateTimeFormat("ru-RU", {
        weekday: "short",
        day: "numeric",
        month: "long",
        hour: "2-digit",
        minute: "2-digit",
    });
    const shortTimeFormatter = new Intl.DateTimeFormat("ru-RU", {
        hour: "2-digit",
        minute: "2-digit",
    });

    async function deleteSelected() {
        if (!selected?.canDelete) return;

        try {
            await deleteCalendarEvent(selected.id);
            selected = null;
            await load();
        } catch (err) {
            console.error("Не удалось удалить событие", err);
            error = "Не удалось удалить событие";
        }
    }

    function openSong(songId: string) {
        selected = null;
        goto(resolve("/app/songs/[id]", { id: songId }));
    }

    // ---------- Создание личного события ----------

    let isCreateOpen = $state(false);
    let newTitle = $state("");
    let newDate = $state("");
    let newStart = $state("18:00");
    let newEnd = $state("19:00");
    let newLocation = $state("");
    let newDescription = $state("");
    let createError = $state<string | null>(null);

    function toDateInput(value: Date): string {
        const y = value.getFullYear();
        const m = String(value.getMonth() + 1).padStart(2, "0");
        const d = String(value.getDate()).padStart(2, "0");
        return `${y}-${m}-${d}`;
    }

    function openCreate() {
        newTitle = "";
        newDate = toDateInput(date);
        newStart = "18:00";
        newEnd = "19:00";
        newLocation = "";
        newDescription = "";
        createError = null;
        isCreateOpen = true;
    }

    async function handleCreate() {
        if (!newTitle || !newDate || !newStart || !newEnd) {
            createError = "Заполните название, дату и время";
            return;
        }

        const startAt = new Date(`${newDate}T${newStart}`);
        const endAt = new Date(`${newDate}T${newEnd}`);
        if (endAt <= startAt) {
            createError = "Окончание должно быть позже начала";
            return;
        }

        try {
            await createCalendarEvent({
                title: newTitle,
                description: newDescription || undefined,
                startAt: startAt.toISOString(),
                endAt: endAt.toISOString(),
                location: newLocation || undefined,
                eventType: "Personal",
            });
            isCreateOpen = false;
            scope = "mine";
            date = startAt;
            await load();
        } catch (err) {
            console.error("Не удалось создать событие", err);
            createError = "Не удалось создать событие";
        }
    }
</script>

<div class="flex h-full flex-col">
    <div class="flex flex-wrap items-center justify-between gap-2 p-3">
        <Tabs.Root value={scope} onValueChange={onScopeChange}>
            <Tabs.List>
                <Tabs.Trigger value="mine">Мои</Tabs.Trigger>
                <Tabs.Trigger value="club">Музклуб</Tabs.Trigger>
            </Tabs.List>
        </Tabs.Root>

        <Tabs.Root
            value={view}
            onValueChange={(value) => (view = value as View)}
        >
            <Tabs.List>
                <Tabs.Trigger value="day">День</Tabs.Trigger>
                <Tabs.Trigger value="week">Неделя</Tabs.Trigger>
            </Tabs.List>
        </Tabs.Root>
    </div>

    {#if error}
        <p class="px-3 pb-2 text-sm text-destructive">{error}</p>
    {/if}

    <div class="min-h-0 flex-1">
        <Timetable
            bind:date
            days={view === "day" ? 1 : 7}
            {events}
            {startHour}
            {endHour}
            hourHeight={view === "day" ? 80 : 56}
            {loading}
            onSelect={(event) => (selected = event)}
            onDaySelect={() => (view = "day")}
        />
    </div>
</div>

<Button
    class="fixed right-4 bottom-18 z-50 rounded-full shadow-lg"
    size="icon"
    aria-label="Создать новое событие"
    onclick={openCreate}
>
    <Plus />
</Button>

<Dialog.Root
    open={selected !== null}
    onOpenChange={(open) => {
        if (!open) selected = null;
    }}
>
    <Dialog.Content>
        {#if selected}
            <Dialog.Header>
                <Dialog.Title>{selected.title}</Dialog.Title>
                <Dialog.Description>
                    {kindLabel[selected.kind]}
                    {#if selected.status && statusLabel[selected.status]}
                        · {statusLabel[selected.status]}
                    {/if}
                </Dialog.Description>
            </Dialog.Header>

            <div class="grid gap-1 text-sm">
                <p>
                    {timeFormatter.format(selected.startAt)} – {shortTimeFormatter.format(
                        selected.endAt,
                    )}
                </p>
                {#if selected.location}
                    <p class="text-muted-foreground">{selected.location}</p>
                {/if}
            </div>

            <Dialog.Footer>
                {#if selected.canDelete}
                    <Button variant="destructive" onclick={deleteSelected}>
                        Удалить
                    </Button>
                {/if}
                {#if selected.songId}
                    {@const songId = selected.songId}
                    <Button onclick={() => openSong(songId)}>
                        Открыть песню
                    </Button>
                {/if}
            </Dialog.Footer>
        {/if}
    </Dialog.Content>
</Dialog.Root>

<Dialog.Root bind:open={isCreateOpen}>
    <Dialog.Content>
        <Dialog.Header>
            <Dialog.Title>Новое событие</Dialog.Title>
            <Dialog.Description>
                Личное событие видно только вам
            </Dialog.Description>
        </Dialog.Header>

        <div class="grid gap-4 py-2">
            <div class="grid gap-2">
                <Label for="title">Название *</Label>
                <Input
                    id="title"
                    bind:value={newTitle}
                    placeholder="Например: Пара по матанализу"
                />
            </div>

            <div class="grid grid-cols-3 gap-2">
                <div class="grid gap-2">
                    <Label for="date">Дата *</Label>
                    <Input id="date" type="date" bind:value={newDate} />
                </div>
                <div class="grid gap-2">
                    <Label for="start">Начало *</Label>
                    <Input id="start" type="time" bind:value={newStart} />
                </div>
                <div class="grid gap-2">
                    <Label for="end">Конец *</Label>
                    <Input id="end" type="time" bind:value={newEnd} />
                </div>
            </div>

            <div class="grid gap-2">
                <Label for="location">Место</Label>
                <Input id="location" bind:value={newLocation} />
            </div>

            <div class="grid gap-2">
                <Label for="description">Описание</Label>
                <Textarea id="description" bind:value={newDescription} rows={3} />
            </div>

            {#if createError}
                <p class="text-sm text-destructive">{createError}</p>
            {/if}
        </div>

        <Dialog.Footer>
            <Button variant="outline" onclick={() => (isCreateOpen = false)}>
                Отмена
            </Button>
            <Button onclick={handleCreate}>Создать</Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
