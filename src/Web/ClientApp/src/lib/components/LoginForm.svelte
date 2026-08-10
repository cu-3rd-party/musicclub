<script lang="ts">
    import {goto} from "$app/navigation";
    import {resolve} from "$app/paths";
    import {
        FieldGroup,
        Field,
        FieldLabel,
        FieldDescription,
        FieldSeparator,
    } from "$lib/components/ui/field/index.js";
    import * as Alert from "$lib/components/ui/alert/index.js";
    import {getDeeplink, getApiErrorMessage} from "$lib/api/auth";
    import {setStoredAuthSession} from "$lib/auth/storage";
    import {Input} from "$lib/components/ui/input/index.js";
    import {Button} from "$lib/components/ui/button/index.js";
    import {cn, type WithElementRef} from "$lib/utils.js";
    import type {HTMLFormAttributes} from "svelte/elements";
    import * as Card from "$lib/components/ui/card/index.js";
    import type {Deeplink} from "$lib/auth/types";

    let {
        ref = $bindable(null),
        class: className,
        deeplink,
        ...restProps
    }: WithElementRef<HTMLFormAttributes> & {deeplink: Deeplink} = $props();

    let errorMessage = $state("");
    let isPolling = $state(false);

    const POLL_INTERVAL_MS = 2000;

    async function handleTelegramLogin(): Promise<void> {
        if (isPolling) {
            return;
        }

        errorMessage = "";
        isPolling = true;

        try {
            window.open(deeplink.url, "_blank");

            const session = await pollForSession();
            if (session) {
                setStoredAuthSession({
                    ...session,
                    accessTokenAcquiredAt: new Date().toISOString(),
                });
                await goto(resolve("/app"));
            }
        } catch (error) {
            errorMessage = getApiErrorMessage(error, "Не удалось выполнить вход.");
        } finally {
            isPolling = false;
        }
    }

    function pollForSession(): Promise<import("$lib/auth/types").AuthSession | null> {
        return new Promise((resolve) => {
            const intervalId = window.setInterval(async () => {
                try {
                    const session = await getDeeplink(deeplink);
                    if (session) {
                        window.clearInterval(intervalId);
                        resolve(session);
                    }
                } catch {
                    window.clearInterval(intervalId);
                    resolve(null);
                }
            }, POLL_INTERVAL_MS);
        });
    }
</script>

<form class={cn("flex flex-col gap-6", className)} bind:this={ref} {...restProps}>
    <Card.Root>
        <Card.Content>
            <FieldGroup>
                <div class="flex flex-col items-center gap-1 text-center">
                    <h1 class="text-2xl font-bold">Войти в систему</h1>
                </div>
                {#if errorMessage}
                    <Alert.Root variant="destructive">
                        <Alert.Description>{errorMessage}</Alert.Description>
                    </Alert.Root>
                {/if}
                <Field>
                    <Button
                        variant="outline"
                        type="button"
                        disabled={isPolling}
                        onclick={handleTelegramLogin}
                    >
                        <svg width="800px" height="800px" viewBox="0 0 48 48" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M41.4193 7.30899C41.4193 7.30899 45.3046 5.79399 44.9808 9.47328C44.8729 10.9883 43.9016 16.2908 43.1461 22.0262L40.5559 39.0159C40.5559 39.0159 40.3401 41.5048 38.3974 41.9377C36.4547 42.3705 33.5408 40.4227 33.0011 39.9898C32.5694 39.6652 24.9068 34.7955 22.2086 32.4148C21.4531 31.7655 20.5897 30.4669 22.3165 28.9519L33.6487 18.1305C34.9438 16.8319 36.2389 13.8019 30.8426 17.4812L15.7331 27.7616C15.7331 27.7616 14.0063 28.8437 10.7686 27.8698L3.75342 25.7055C3.75342 25.7055 1.16321 24.0823 5.58815 22.459C16.3807 17.3729 29.6555 12.1786 41.4193 7.30899Z"
                                  fill="#FFFFFF"/>
                        </svg>
                        {isPolling ? "Ожидание подтверждения..." : "Зайти через тг"}
                    </Button>
                </Field>
            </FieldGroup>
        </Card.Content>
    </Card.Root>
</form>
