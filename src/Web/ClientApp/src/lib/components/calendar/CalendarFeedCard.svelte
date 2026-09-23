<script lang="ts">
    import type { CalendarFeed } from "$lib/api/calendar";
    import { Copy, RefreshCw, Trash2, Check } from "@lucide/svelte";
    import { Button } from "$lib/components/ui/button";
    import {
        Card,
        CardContent,
        CardDescription,
        CardFooter,
        CardHeader,
        CardTitle,
    } from "$lib/components/ui/card";
    import * as Dialog from "$lib/components/ui/dialog";
    import { createEventDispatcher } from "svelte";

    let { feed }: { feed: CalendarFeed | null } = $props();

    const dispatch = createEventDispatcher<{
        copy: void;
        regenerate: void;
        revoke: void;
    }>();

    let isCopied = $state(false);
    let isRegenerateDialogOpen = $state(false);
    let isRevokeDialogOpen = $state(false);

    const handleCopy = () => {
        if (feed?.icsUrl) {
            navigator.clipboard.writeText(feed.icsUrl);
            isCopied = true;
            dispatch("copy");
            setTimeout(() => (isCopied = false), 2000);
        }
    };

    const handleRegenerate = () => {
        dispatch("regenerate");
        isRegenerateDialogOpen = false;
    };

    const handleRevoke = () => {
        dispatch("revoke");
        isRevokeDialogOpen = false;
    };
</script>

<Card>
    <CardHeader>
        <CardTitle class="flex items-center gap-2">
            <span>📬</span>
            Персональный календарь
        </CardTitle>
        <CardDescription>
            Ваша персональная ссылка для подписки на календарь в формате ICS
        </CardDescription>
    </CardHeader>

    <CardContent class="space-y-4">
        {#if feed && feed.isActive}
            <div class="space-y-2">
                <label for="feed-url" class="text-sm font-medium">Ваша ссылка:</label>
                <div class="flex items-center gap-2">
                    <input
                        id="feed-url"
                        type="text"
                        readonly
                        value={feed.icsUrl}
                        class="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background file:border-0 file:bg-transparent file:text-sm file:font-medium placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                    />
                    <Button
                        variant="outline"
                        size="icon"
                        onclick={handleCopy}
                        title="Скопировать ссылку"
                    >
                        {#if isCopied}
                            <Check class="h-4 w-4 text-green-500" />
                        {:else}
                            <Copy class="h-4 w-4" />
                        {/if}
                    </Button>
                </div>
            </div>

            <div class="flex items-center gap-2">
                <Dialog.Root open={isRegenerateDialogOpen} onOpenChange={(open) => { isRegenerateDialogOpen = open; }}>
                    <Dialog.Trigger>
                        <Button variant="outline" size="sm">
                            <RefreshCw class="mr-2 h-4 w-4" />
                            Пересоздать
                        </Button>
                    </Dialog.Trigger>
                    <Dialog.Content>
                        <Dialog.Header>
                            <Dialog.Title>Пересоздать календарный фид?</Dialog.Title>
                            <Dialog.Description>
                                Старая ссылка перестанет работать. Все устройства, использующие эту ссылку,
                                нужно будет обновить. Это действие нельзя отменить.
                            </Dialog.Description>
                        </Dialog.Header>
                        <Dialog.Footer>
                            <Button variant="outline" onclick={() => (isRegenerateDialogOpen = false)}>
                                Отмена
                            </Button>
                            <Button variant="destructive" onclick={handleRegenerate}>
                                Пересоздать
                            </Button>
                        </Dialog.Footer>
                    </Dialog.Content>
                </Dialog.Root>

                <Dialog.Root open={isRevokeDialogOpen} onOpenChange={(open) => { isRevokeDialogOpen = open; }}>
                    <Dialog.Trigger>
                        <Button variant="destructive" size="sm">
                            <Trash2 class="mr-2 h-4 w-4" />
                            Отозвать
                        </Button>
                    </Dialog.Trigger>
                    <Dialog.Content>
                        <Dialog.Header>
                            <Dialog.Title>Отозвать календарный фид?</Dialog.Title>
                            <Dialog.Description>
                                Ссылка будет деактивирована. Вы сможете создать новый фид позже.
                                Это действие нельзя отменить.
                            </Dialog.Description>
                        </Dialog.Header>
                        <Dialog.Footer>
                            <Button variant="outline" onclick={() => (isRevokeDialogOpen = false)}>
                                Отмена
                            </Button>
                            <Button variant="destructive" onclick={handleRevoke}>
                                Отозвать
                            </Button>
                        </Dialog.Footer>
                    </Dialog.Content>
                </Dialog.Root>
            </div>
        {:else}
            <div class="flex flex-col items-center justify-center rounded-lg border border-dashed bg-muted/30 p-8 text-center">
                <p class="text-sm text-muted-foreground">
                    {feed?.revokedAt
                        ? "Фид был отозван"
                        : "У вас нет активного календарного фида"}
                </p>
            </div>
        {/if}
    </CardContent>

    <CardFooter>
        {#if isCopied}
            <p class="text-sm text-green-600">Ссылка скопирована в буфер обмена!</p>
        {/if}
    </CardFooter>
</Card>
