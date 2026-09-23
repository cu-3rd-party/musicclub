<script lang="ts">
    import { onMount } from "svelte";
    import { Button } from "$lib/components/ui/button";
    import {
        Card,
        CardContent,
        CardDescription,
        CardHeader,
        CardTitle,
    } from "$lib/components/ui/card";
    import * as Dialog from "$lib/components/ui/dialog";
    import { Input } from "$lib/components/ui/input";
    import { Label } from "$lib/components/ui/label";
    import { Textarea } from "$lib/components/ui/textarea";
    import { CalendarFeedCard, CalendarEventList } from "$lib/components/calendar";
    import {
        getCalendarFeed,
        regenerateCalendarFeed,
        revokeCalendarFeed,
        getCalendarEvents,
        createCalendarEvent,
        deleteCalendarEvent,
        type CalendarFeed,
        type CalendarEvent,
        type CreateCalendarEventRequest,
        type CalendarEventType,
    } from "$lib/api/calendar";
    import { Plus, Trash2, ExternalLink } from "@lucide/svelte";

    let feed = $state<CalendarFeed | null>(null);
    let events = $state<CalendarEvent[]>([]);
    let loading = $state(true);
    let error = $state<string | null>(null);

    // Create event dialog
    let isCreateDialogOpen = $state(false);
    let newEventTitle = $state("");
    let newEventDescription = $state("");
    let newEventStartAt = $state("");
    let newEventEndAt = $state("");
    let newEventLocation = $state("");
    let newEventType = $state<CalendarEventType>("Personal");

    // Event deletion
    let eventToDelete = $state<string | null>(null);

    async function loadFeed() {
        try {
            feed = await getCalendarFeed();
        } catch (err) {
            console.error("Не удалось загрузить фид", err);
            feed = null;
        }
    }

    async function loadEvents() {
        try {
            const now = new Date();
            const from = now.toISOString();
            const to = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000).toISOString(); // +30 дней

            events = await getCalendarEvents(from, to);
        } catch (err) {
            console.error("Не удалось загрузить события", err);
            events = [];
        }
    }

    async function handleRegenerate() {
        try {
            feed = await regenerateCalendarFeed();
            await loadEvents();
        } catch (err) {
            console.error("Не удалось перевыпустить фид", err);
            error = "Не удалось перевыпустить фид";
        }
    }

    async function handleRevoke() {
        try {
            await revokeCalendarFeed();
            feed = null;
            await loadEvents();
        } catch (err) {
            console.error("Не удалось отозвать фид", err);
            error = "Не удалось отозвать фид";
        }
    }

    async function handleCreateEvent() {
        if (!newEventTitle || !newEventStartAt || !newEventEndAt) {
            error = "Заполните обязательные поля";
            return;
        }

        try {
            const payload: CreateCalendarEventRequest = {
                title: newEventTitle,
                description: newEventDescription || undefined,
                startAt: newEventStartAt,
                endAt: newEventEndAt,
                location: newEventLocation || undefined,
                eventType: newEventType,
            };

            await createCalendarEvent(payload);
            isCreateDialogOpen = false;
            resetForm();
            await loadEvents();
        } catch (err) {
            console.error("Не удалось создать событие", err);
            error = "Не удалось создать событие";
        }
    }

    async function handleDeleteEvent() {
        if (!eventToDelete) return;

        try {
            await deleteCalendarEvent(eventToDelete);
            eventToDelete = null;
            await loadEvents();
        } catch (err) {
            console.error("Не удалось удалить событие", err);
            error = "Не удалось удалить событие";
        }
    }

    function resetForm() {
        newEventTitle = "";
        newEventDescription = "";
        newEventStartAt = "";
        newEventEndAt = "";
        newEventLocation = "";
        newEventType = "Personal";
        error = null;
    }

    onMount(() => {
        async function init() {
            loading = true;
            await Promise.all([loadFeed(), loadEvents()]);
            loading = false;
        }

        init();
    });
</script>

<main class="w-full h-full flex flex-col px-4 py-4 overflow-auto">
    <div class="mb-6">
        <h1 class="text-2xl font-bold">📅 Мой календарь</h1>
        <p class="text-sm text-muted-foreground">
            Управление персональным календарём и предстоящие события
        </p>
    </div>

    {#if loading}
        <div class="flex-1 flex items-center justify-center">
            <p class="text-muted-foreground">Загрузка...</p>
        </div>
    {:else}
        <div class="grid gap-6 md:grid-cols-2">
            <!-- Левая колонка: Календарный фид -->
            <div class="space-y-6">
                <CalendarFeedCard
                    feed={feed}
                    on:copy
                    on:regenerate={handleRegenerate}
                    on:revoke={handleRevoke}
                />

                <!-- Создание личного события -->
                <Card>
                    <CardHeader>
                        <CardTitle class="flex items-center gap-2">
                            <Plus class="h-5 w-5" />
                            Личные события
                        </CardTitle>
                        <CardDescription>
                            Создавайте личные события, которые будут отображаться в вашем календаре
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        <Dialog.Root open={isCreateDialogOpen} onOpenChange={(open) => { isCreateDialogOpen = open; }}>
                            <Dialog.Trigger>
                                <Button class="w-full">
                                    <Plus class="mr-2 h-4 w-4" />
                                    Добавить событие
                                </Button>
                            </Dialog.Trigger>
                            <Dialog.Content>
                                <Dialog.Header>
                                    <Dialog.Title>Новое событие</Dialog.Title>
                                    <Dialog.Description>
                                        Заполните информацию о событии
                                    </Dialog.Description>
                                </Dialog.Header>

                                <div class="grid gap-4 py-4">
                                    <div class="grid gap-2">
                                        <Label for="title">Название *</Label>
                                        <Input
                                            id="title"
                                            bind:value={newEventTitle}
                                            placeholder="Например: Визит к стоматологу"
                                        />
                                    </div>

                                    <div class="grid grid-cols-2 gap-4">
                                        <div class="grid gap-2">
                                            <Label for="startAt">Начало *</Label>
                                            <Input
                                                id="startAt"
                                                type="datetime-local"
                                                bind:value={newEventStartAt}
                                            />
                                        </div>
                                        <div class="grid gap-2">
                                            <Label for="endAt">Окончание *</Label>
                                            <Input
                                                id="endAt"
                                                type="datetime-local"
                                                bind:value={newEventEndAt}
                                            />
                                        </div>
                                    </div>

                                    <div class="grid gap-2">
                                        <Label for="location">Место</Label>
                                        <Input
                                            id="location"
                                            bind:value={newEventLocation}
                                            placeholder="Например: ул. Ленина, 1"
                                        />
                                    </div>

                                    <div class="grid gap-2">
                                        <Label for="description">Описание</Label>
                                        <Textarea
                                            id="description"
                                            bind:value={newEventDescription}
                                            placeholder="Дополнительная информация"
                                            rows={3}
                                        />
                                    </div>

                                    <div class="grid gap-2">
                                        <Label>Тип события</Label>
                                        <div class="flex gap-2">
                                            <Button
                                                variant={newEventType === "Personal" ? "default" : "outline"}
                                                size="sm"
                                                onclick={() => (newEventType = "Personal")}
                                            >
                                                Личное
                                            </Button>
                                        </div>
                                    </div>

                                    {#if error}
                                        <p class="text-sm text-destructive">{error}</p>
                                    {/if}
                                </div>

                                <Dialog.Footer>
                                    <Button variant="outline" onclick={() => { resetForm(); isCreateDialogOpen = false; }}>
                                        Отмена
                                    </Button>
                                    <Button onclick={handleCreateEvent}>
                                        Создать
                                    </Button>
                                </Dialog.Footer>
                            </Dialog.Content>
                        </Dialog.Root>
                    </CardContent>
                </Card>
            </div>

            <!-- Правая колонка: Список событий -->
            <div class="space-y-6">
                <Card>
                    <CardHeader>
                        <CardTitle class="flex items-center gap-2">
                            <ExternalLink class="h-5 w-5" />
                            Предстоящие события
                        </CardTitle>
                        <CardDescription>
                            Ближайшие события из вашего календаря
                        </CardDescription>
                    </CardHeader>
                    <CardContent>
                        {#if events.length === 0}
                            <div class="flex flex-col items-center justify-center rounded-lg border border-dashed bg-muted/30 p-8 text-center">
                                <p class="text-sm text-muted-foreground">Нет предстоящих событий</p>
                            </div>
                        {:else}
                            <div class="space-y-3">
                                {#each events.slice(0, 10) as event (event.id)}
                                    <div class="relative group">
                                        <CalendarEventList events={[event]} />
                                        {#if event.sourceType === "Personal"}
                                            <button
                                                class="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity p-1 hover:bg-destructive/10 rounded"
                                                title="Удалить событие"
                                                onclick={() => (eventToDelete = event.id)}
                                            >
                                                <Trash2 class="h-4 w-4 text-destructive" />
                                            </button>
                                        {/if}
                                    </div>
                                {/each}

                                {#if events.length > 10}
                                    <div class="flex justify-center pt-2">
                                        <Button variant="outline" size="sm">
                                            Показать все ({events.length})
                                        </Button>
                                    </div>
                                {/if}
                            </div>
                        {/if}
                    </CardContent>
                </Card>
            </div>
        </div>
    {/if}

    <!-- Диалог удаления события -->
    <Dialog.Root open={!!eventToDelete} onOpenChange={(open) => {
        if (!open) eventToDelete = null;
    }}>
        <Dialog.Content>
            <Dialog.Header>
                <Dialog.Title>Удалить событие?</Dialog.Title>
                <Dialog.Description>
                    Это действие нельзя отменить. Событие будет удалено из вашего календаря.
                </Dialog.Description>
            </Dialog.Header>
            <Dialog.Footer>
                <Button variant="outline" onclick={() => (eventToDelete = null)}>
                    Отмена
                </Button>
                <Button variant="destructive" onclick={handleDeleteEvent}>
                    Удалить
                </Button>
            </Dialog.Footer>
        </Dialog.Content>
    </Dialog.Root>
</main>
