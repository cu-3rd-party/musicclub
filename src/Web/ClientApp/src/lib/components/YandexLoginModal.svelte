<script lang="ts">
    import * as Dialog from "$lib/components/ui/dialog";
    import {Button} from "$lib/components/ui/button";
    import {Input} from "$lib/components/ui/input";
    import {Label} from "$lib/components/ui/label";
    import {Check, X} from "@lucide/svelte";
    import {updateYandexLogin, type YandexLoginDto} from "$lib/api/yandex-login";

    export let open = $state(false);
    export let yandexLogin: YandexLoginDto | null = null;

    let input = $state("");
    let saving = $state(false);
    let error = $state<string | null>(null);

    const emit = defineEmits<{
        (e: "saved", login: YandexLoginDto): void;
        (e: "close"): void;
    }>();

    function handleSubmit() {
        saving = true;
        error = null;

        updateYandexLogin(input.trim() || null)
            .then((result) => {
                open = false;
                emit("saved", result);
            })
            .catch((err) => {
                console.error("Failed to set YandexLogin:", err);
                error = "Не удалось сохранить. Попробуйте еще раз.";
            })
            .finally(() => {
                saving = false;
            });
    }

    function handleClose() {
        open = false;
        emit("close");
    }

    $: if (open) {
        input = yandexLogin?.yandexLogin ?? "";
        error = null;
    }
</script>

<Dialog.Root bind:open>
    <Dialog.Content class="sm:max-w-md">
        <Dialog.Header>
            <Dialog.Title>Настройте Яндекс.Почту</Dialog.Title>
            <Dialog.Description>
                Для проверки доступности участников укажите ваш логин из корпоративной почты
                @edu.centraluniversity.ru
            </Dialog.Description>
        </Dialog.Header>

        <div class="space-y-4 py-4">
            <div class="space-y-2">
                <Label for="yandex-login-modal">
                    Логин Яндекса
                </Label>
                <Input
                    id="yandex-login-modal"
                    value={input}
                    placeholder="ivan.ivanov"
                    onkeydown={(e) => {
                        if (e.key === "Enter") handleSubmit();
                        if (e.key === "Escape") handleClose();
                    }}
                />
                <p class="text-xs text-muted-foreground">
                    Введите только логин (без @edu.centraluniversity.ru)
                </p>
                {#if error}
                    <p class="text-sm text-destructive">{error}</p>
                {/if}
            </div>
        </div>

        <Dialog.Footer>
            <Button variant="outline" onclick={handleClose} disabled={saving}>
                Позже
            </Button>
            <Button onclick={handleSubmit} disabled={saving}>
                {#if saving}
                    <span class="animate-spin mr-2">⏳</span>
                    Сохранение...
                {:else}
                    <Check class="size-4 mr-2" />
                    Сохранить
                {/if}
            </Button>
        </Dialog.Footer>
    </Dialog.Content>
</Dialog.Root>
